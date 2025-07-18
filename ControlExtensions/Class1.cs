using System.Reflection;

public static class ControlExtensions
{
    public static T DeepClone<T>(this T source) where T : Control
    {
        T clone = (T)Activator.CreateInstance(source.GetType())!;
        CloneProperties(source, clone);

        if (source is SplitContainer srcSplit && clone is SplitContainer dstSplit)
        {
            foreach (Control child in srcSplit.Panel1.Controls)
                dstSplit.Panel1.Controls.Add(child.DeepClone());

            foreach (Control child in srcSplit.Panel2.Controls)
                dstSplit.Panel2.Controls.Add(child.DeepClone());

            return clone;
        }

        // Clone children except for special cases
        if (source is not TableLayoutPanel && source is not MenuStrip)
        {
            foreach (Control child in source.Controls)
                clone.Controls.Add(child.DeepClone());
        }

        // Clone MenuStrip items
        if (source is MenuStrip srcMenu && clone is MenuStrip dstMenu)
        {
            foreach (ToolStripItem item in srcMenu.Items)
            {
                if (item is ToolStripMenuItem menuItem)
                {
                    dstMenu.Items.Add(DeepCloneMenuItem(menuItem));
                }
            }
        }

        FixSplitterPanelSizes(clone);
        return clone;
    }

    private static ToolStripMenuItem DeepCloneMenuItem(ToolStripMenuItem source)
    {
        var clone = new ToolStripMenuItem
        {
            Name = source.Name,
            Text = source.Text,
            Enabled = source.Enabled,
            Checked = source.Checked,
            CheckOnClick = source.CheckOnClick,
            ShortcutKeys = source.ShortcutKeys,
            ShowShortcutKeys = source.ShowShortcutKeys,
            Image = source.Image,
            ImageScaling = source.ImageScaling
        };

        foreach (ToolStripItem item in source.DropDownItems)
        {
            if (item is ToolStripMenuItem subItem)
            {
                subItem.Visible = !item.Visible;
                clone.DropDownItems.Add(DeepCloneMenuItem(subItem));
            }
        }

        return clone;
    }


    private static void CloneProperties(Control source, Control target)
    {
        var properties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.Name != "WindowTarget" && p.Name != "Parent" && p.Name != "Visible");

        foreach (var prop in properties)
        {
            try
            {
                var value = prop.GetValue(source);
                prop.SetValue(target, value);
            }
            catch { }
        }

        if (source is TableLayoutPanel src && target is TableLayoutPanel dst)
        {
            dst.ColumnCount = src.ColumnCount;
            dst.RowCount = src.RowCount;

            dst.ColumnStyles.Clear();
            foreach (ColumnStyle col in src.ColumnStyles)
                dst.ColumnStyles.Add(new ColumnStyle(col.SizeType, col.Width));

            dst.RowStyles.Clear();
            foreach (RowStyle row in src.RowStyles)
                dst.RowStyles.Add(new RowStyle(row.SizeType, row.Height));

            foreach (Control child in src.Controls)
            {
                var clonedChild = child.DeepClone();
                var pos = src.GetPositionFromControl(child);
                dst.Controls.Add(clonedChild, pos.Column, pos.Row);
            }

            return; // prevents double-adding in outer loop
        }
    }

    public static void FixSplitterPanelSizes(Control root)
    {
        if (root is SplitContainer split)
        {
            split.SplitterDistance = split.Orientation == Orientation.Horizontal
                ? int.MaxValue / 2
                : 0;

            // Respect existing MinSize values (copied during DeepClone)
            split.Panel1MinSize = Math.Max(1, split.Panel1MinSize);
            split.Panel2MinSize = Math.Max(1, split.Panel2MinSize);
        }

        foreach (Control child in root.Controls)
            FixSplitterPanelSizes(child);
    }
}
