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
        private bool WrapAround { get; set; }
        private bool WaitingForPress { get; set; }
        private byte WaitingForPressRegisterIndex { get; set; }
        private bool Paused { get; set; }
        Func<Task> PlaySound { get; set; }
        private int Speed { get; set; }
        private int Counter { get; set; }

        public Processor(byte[] romData, bool wrapAround, Func<Task> playSound, int speed)
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

            WrapAround = wrapAround;

            WaitingForPress = false;
            WaitingForPressRegisterIndex = 0;
            Paused = false;

            PlaySound = playSound;

            Speed = speed;
            Counter = 0;

            LoadRom(romData);
            LoadFonts();
        }

        public void LoadRom(byte[] romData)
        {
            Array.Copy(romData, 0, Memory, MEMORY_START, romData.Length);
        }

        public void Cycle()
        {
            if (Paused)
            {
                return;
            }

            var opCode = (ushort)((Memory[PC] << 8) | Memory[PC + 1]);
            PC += 2;

            var NNN = (ushort)(opCode & 0x0FFF);
            var KK = (byte)(opCode & 0x00FF);
            var N = (byte)(opCode & 0x000F);
            var X = (byte)((opCode & 0x0F00) >> 8);
            var Y = (byte)((opCode & 0x00F0) >> 4);

            switch (opCode & 0xF000)
            {
                case 0x0000 when opCode == 0x00E0:
                    // CLS
                    Pixels = new bool[2048];
                    break;
                case 0x0000 when opCode == 0x00EE:
                    // RET
                    PC = Stack[SP];
                    SP -= 1;
                    break;
                case 0x1000:
                    // JP addr
                    PC = NNN;
                    break;
                case 0x2000:
                    // CALL addr
                    SP += 1;
                    Stack[SP] = PC;
                    PC = NNN;
                    break;
                case 0x3000:
                    // SE Vx, byte
                    if (V[X] == KK)
                    {
                        PC += 2;
                    }
                    break;
                case 0x4000:
                    // SNE Vx, byte
                    if (V[X] != KK)
                    {
                        PC += 2;
                    }
                    break;
                case 0x5000:
                    // SE Vx, Vy
                    if (V[X] == V[Y])
                    {
                        PC += 2;
                    }
                    break;
                case 0x6000:
                    // LD Vx, byte
                    V[X] = KK;
                    break;
                case 0x7000:
                    // ADD Vx, byte
                    V[X] += KK;
                    break;
                case 0x8000 when (opCode & 0xF) == 0x0:
                    // LD Vx, Vy
                    V[X] = V[Y];
                    break;
                case 0x8000 when (opCode & 0xF) == 0x1:
                    // OR Vx, Vy
                    V[X] |= V[Y];
                    break;
                case 0x8000 when (opCode & 0xF) == 0x2:
                    // AND Vx, Vy
                    V[X] &= V[Y];
                    break;
                case 0x8000 when (opCode & 0xF) == 0x3:
                    // XOR Vx, Vy
                    V[X] ^= V[Y];
                    break;
                case 0x8000 when (opCode & 0xF) == 0x4:
                    // ADD Vx, Vy
                    var sum = (V[X] += V[Y]);
                    V[0xF] = (sum > 255) ? (byte)1 : (byte)0;
                    V[X] = (byte)(sum & 0xFF);
                    break;
                case 0x8000 when (opCode & 0xF) == 0x5:
                    // SUB Vx, Vy
                    V[0xF] = V[X] > V[Y] ? (byte)1 : (byte)0;
                    V[X] -= V[Y];
                    break;
                case 0x8000 when (opCode & 0xF) == 0x6:
                    // SHR Vx {, Vy}
                    V[0xF] = (byte)(V[X] & 0x1);

                    // Divide by 2. Right shift by 1
                    V[X] >>= 0x1;
                    break;
                case 0x8000 when (opCode & 0xF) == 0x7:
                    // SUBN Vx, Vy
                    V[0xF] = V[Y] > V[X] ? (byte)1 : (byte)0;
                    V[X] = (byte)(V[Y] - V[X]);
                    break;
                case 0x8000 when (opCode & 0xF) == 0xE:
                    // SHL Vx {, VY}
                    V[0xF] = (byte)((V[X] & 0x80) >> 7);

                    // Multiply by 2. Left shift by 1
                    V[X] <<= 0x1;
                    break;
                case 0x9000:
                    // SNE Vx, Vy
                    if (V[X] != V[Y])
                    {
                        PC += 2;
                    }
                    break;
                case 0xA000:
                    // LD I, addr
                    I = NNN;
                    break;
                case 0xB000:
                    // JP V0, addr
                    PC = (byte)(NNN + V[0]);
                    break;
                case 0xC000:
                    // RND Vx, byte
                    var random = new Random();
                    var randByte = (random.Next(0, 256) & KK);
                    V[X] = (byte)randByte;
                    break;
                case 0xD000:
                    // DRW Vx, Vy, nibble

                    var width = 8;
                    var height = N;

                    V[0xF] = 0;

                    for (var row = 0; row < height; row++)
                    {
                        var sprite = Memory[I + row];

                        for (var col = 0; col < width; col++)
                        {
                            if ((sprite & 0x80) > 0)
                            {
                                int x = V[X] + col;
                                int y = V[Y] + row;

                                if (!WrapAround && (x >= 64 || y >= 32))
                                {
                                    continue;
                                }
                                else
                                {
                                    x %= 64;
                                    y %= 32;
                                }

                                var index = x + (y * 64);

                                if (Pixels[index] == true)
                                {
                                    V[0xF] = 1;
                                }

                                Pixels[index] = !Pixels[index];
                            }

                            sprite <<= 1;
                        }
                    }
                    break;
                case 0xE000 when (opCode & 0xFF) == 0x9E:
                    // SKP Vx
                    if (KeysDown[V[X]])
                    {
                        PC += 2;
                    }
                    break;
                case 0xE000 when (opCode & 0xFF) == 0xA1:
                    // SKNP Vx
                    if (!KeysDown[V[X]])
                    {
                        PC += 2;
                    }
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x07:
                    // LD Vx, DT
                    V[X] = (byte)DelayTimer;
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x0A:
                    // LD Vx, K
                    Paused = true;
                    WaitingForPress = true;
                    WaitingForPressRegisterIndex = X;
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x15:
                    // LD DT, Vx
                    DelayTimer = V[X];
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x18:
                    // LD ST, Vx
                    SoundTimer = V[X];
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x1E:
                    // ADD I, Vx
                    I = (ushort)(I + V[X]);
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x29:
                    // LD F, Vx
                    I = (ushort)(V[X] * 5);
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x33:
                    // LD B, Vx
                    var value = V[X];
                    Memory[I] = (byte)(value / 100);
                    Memory[I + 1] = (byte)(value % 100 / 10);
                    Memory[I + 2] = (byte)(value % 10);
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x55:
                    // LD [I], Vx
                    for (var i = 0; i <= X; i++)
                    {
                        Memory[I + i] = V[i];
                    }
                    break;
                case 0xF000 when (opCode & 0xFF) == 0x65:
                    // LD Vx, [I]
                    for (var i = 0; i <= X; i++)
                    {
                        V[i] = Memory[I + i];
                    }
                    break;
                default:
                    throw new Exception($"OpCode not found at PC {PC}");
            }

            if (Counter % Speed == 0)
            {
                DecrementTimers();
            }

            Counter += 1;
        }

        private void DecrementTimers()
        {
            if (DelayTimer > 0)
            {
                DelayTimer -= 1;
            }

            if (SoundTimer > 0)
            {
                SoundTimer -= 1;
                if (SoundTimer == 0)
                {
                    PlaySound();
                }
            }
        }

        public void OnKeyDown(byte byteValue)
        {
            KeysDown[byteValue] = true;

            if (WaitingForPress)
            {
                Paused = false;
                WaitingForPress = false;
                V[WaitingForPressRegisterIndex] = byteValue;
            }
        }

        public void OnKeyUp(byte byteValue)
        {
            KeysDown[byteValue] = false;
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
