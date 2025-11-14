using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using PAKLib;

namespace HBPakEditor
{
    public class PAKTabPage : TabPage
    {
        private string? _filePath = null;
        public string? FilePath
        {
            get { return _filePath; }
            set { _filePath = value; }
        }

        private PAKLib.PAK? _openPAK = null;
        public PAKLib.PAK? OpenPAK
        {
            get { return _openPAK; }
            set { _openPAK = value; }
        }

        private PAKLib.Sprite? _activeSprite = null;
        public PAKLib.Sprite? ActiveSprite
        {
            get { return _activeSprite; }
            set { _activeSprite = value; }
        }

        private byte[] _keyBytes = [];
        public byte[] KeyBytes
        {
            get { return _keyBytes; }
            set { _keyBytes = value; }
        }
    }
}
