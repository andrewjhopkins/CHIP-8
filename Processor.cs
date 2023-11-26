namespace CHIP_8
{
    public class Processor
    {
        private readonly ushort MEMORY_START = 0x200;
        private byte[] Memory { get; set; }
        private ushort PC { get; set; }
        private byte[] V { get; set; }
        private ushort I { get; set; }
        private int DelayTimer { get; set; }
        private int SoundTimer { get; set; }
        private ushort[] Stack { get; set; }
        private byte SP { get; set; }
        internal bool[] Pixels { get; set; }
        private bool[] KeysDown { get; set; }

        public Processor()
        {
            Pixels = new bool[2048];

            Memory = new byte[4096];
            PC = MEMORY_START;

            V = new byte[16];
            I = 0;

            Stack = new ushort[16];
            SP = 0;

            KeysDown = new bool[16];

            DelayTimer = 0;
            SoundTimer = 0;

            LoadFonts();
        }

        public void LoadRom(byte[] romData)
        {
            Array.Copy(romData, 0, Memory, MEMORY_START, romData.Length);
        }

        private void LoadFonts()
        {
            var fonts = new byte[]
            {
                0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
                0x20, 0x60, 0x20, 0x20, 0x70, // 1
                0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
                0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
                0x90, 0x90, 0xF0, 0x10, 0x10, // 4
                0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
                0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
                0xF0, 0x10, 0x20, 0x40, 0x40, // 7
                0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
                0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
                0xF0, 0x90, 0xF0, 0x90, 0x90, // A
                0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
                0xF0, 0x80, 0x80, 0x80, 0xF0, // C
                0xE0, 0x90, 0x90, 0x90, 0xE0, // D
                0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
                0xF0, 0x80, 0xF0, 0x80, 0x80  // F
            };
            Array.Copy(fonts, 0, Memory, 0, fonts.Length);
        }
    }
}
