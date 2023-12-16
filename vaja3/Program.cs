using NAudio.Midi;
using NAudio.Wave;
using System;
using System.Collections;
using System.Diagnostics.Metrics;

namespace vaja4
{
    internal class Program
    {
        public static short[] ReadFile(string path)
        {
            using (WaveFileReader reader = new WaveFileReader(path))
            {
                byte[] buffer = new byte[reader.Length];
                int read = reader.Read(buffer, 0, buffer.Length);
                short[] sampleBuffer = new short[read / 2];
                Buffer.BlockCopy(buffer, 0, sampleBuffer, 0, read);
                return sampleBuffer;
            }
        }

        public static bool IsBitSet(long value, int position)
        {
            return (value & (1 << position)) != 0;
        }

        public static void SetBit(ref int num, int position)
        {
            num |= (1 << position);
        }

        public static void SetLongBit(ref long num, int position)
        {
            num |= (1 << position);
        }

        public static BitArray SetHeader(int n, int m, long samples)
        {
            BitArray header = new BitArray(32 + 32 + 16 + 16);

            int i = 0;

            for (int j = 31; j >= 0; j--)
            {
                header[i] = IsBitSet(samples, j);
                i++;
            }

            for (int j = 31; j >= 0; j--)
            {
                header[i] = IsBitSet(16, j);
                i++;
            }

            for (int j = 15; j >= 0; j--)
            {
                header[i] = IsBitSet(n, j);
                i++;
            }

            for (int j = 15; j >= 0; j--)
            {
                header[i] = IsBitSet(m, j);
                i++;
            }

            return header;
        }

        static void GetHeader(BitArray bits, ref int n, ref int m, ref long samples, ref int frequency)
        {
            int i = 0;
            for (int j = 31; j >= 0; j--)
            {
                if (bits[i]) SetLongBit(ref samples, j);
                i++;
            }

            for (int j = 31; j >= 0; j--)
            {
                if (bits[i]) SetBit(ref frequency, j);
                i++;
            }

            for (int j = 15; j >= 0; j--)
            {
                if (bits[i]) SetBit(ref n, j);
                i++;
            }

            for (int j = 15; j >= 0; j--)
            {
                if (bits[i]) SetBit(ref m, j);
                i++;
            }
        }

        public static int CalculateBits(int number)
        {
            return (int)Math.Log(Math.Abs(number), 2) + 2;
        }

        public static List<double> MDCT(List<double> M, int n, int blockSize, int m)
        {
            Console.WriteLine("stage 0");
            List<double> tmp = M.ConvertAll(x => x);
            M.Clear();
            M = Enumerable.Range(0, n).Select(x => 0.0).ToList();
            M.AddRange(tmp.Take(n));

            for (int i = 0; i < tmp.Count(); i += n)
            {
                for (int j = i; j < blockSize + i && j < tmp.Count(); j++)
                {
                    M.Add(tmp[j]);
                }
            }
            Console.WriteLine("stage 1");

            M.AddRange(M.Take(n));
            List<double> finalised = new List<double>();
            int counter = 0;

            for (int i = 0; i < M.Count(); i++)
            {
                M[i] *= Math.Sin((Math.PI / blockSize) * (counter + 0.5));
                counter++;
                if (counter == blockSize)
                    counter = 0;
            }

            Console.WriteLine("stage 2");
            double xk = 0;
            for (int i = 0; i < M.Count(); i += blockSize)
            {
                tmp = M.Skip(i).Take(blockSize).ToList();
                for (int k = 0; k < n; k++)
                {
                    xk = 0;
                    for (int z = 0; z < tmp.Count(); z++)
                    {
                        xk += tmp[z] * Math.Cos((Math.PI / (double)n) * ((double)z + 0.5 + ((double)n / 2)) * ((double)k + 0.5));
                    }
                    finalised.Add(Math.Truncate(xk));
                }
            }

            Console.WriteLine("stage 3");
            for (int i = finalised.Count() - 1; i >= 0; i -= n)
            {
                for (int j = i; j > i - m; j--)
                {
                    finalised[j] = 0;
                }
            }

            Console.WriteLine("stage 4");
            return finalised;
        }

        public static void Compress(int n, int m)
        {
            short[] united = ReadFile("Red Army Medley.wav");

            List<double> M = new List<double>();
            List<double> S = new List<double>();
            int blockSize = 2 * n;

            for (int i = 0; i < united.Length - 1; i += 2)
            {
                M.Add((united[i] + united[i + 1]) / 2);
                S.Add((united[i] - united[i + 1]) / 2);
            }

            //List<double> M = new List<double> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            long samples = M.Count();

            List<double> finalised = MDCT(M, n, blockSize, m);

            finalised.AddRange(MDCT(S, n, blockSize, m));

            BitArray outArray = SetHeader(n, m, samples);
            int len = outArray.Length;
            int value = 0;
            finalised = finalised.Where(x => x != 0).ToList();

            Console.WriteLine("stage 5");
            for (int i = 0; i < finalised.Count(); i++)
            {
                int bits = CalculateBits((int)finalised[i]);
                outArray.Length += bits + 6;

                for (int j = 5; j >= 0; j--)
                {
                    outArray[len] = IsBitSet(bits, j);
                    len++;
                }

                if (finalised[i] < 0)
                {
                    outArray[len] = true;
                    len++;
                }
                else
                {
                    outArray[len] = false;
                    len++;
                }

                value = Math.Abs((int)finalised[i]);
                for (int j = bits - 2; j >= 0; j--)
                {
                    outArray[len] = IsBitSet(value, j);
                    len++;
                }
            }

            Console.WriteLine("stage 6");
            byte[] bytes = new byte[(outArray.Length - 1) / 8 + 1];
            outArray.CopyTo(bytes, 0);

            for (int i = 0; i < bytes.Length; i++)
            {
                byte originalByte = bytes[i];
                byte reversedByte = 0;

                for (int f = 0; f < 8; f++)
                {
                    if ((originalByte & (1 << f)) != 0)
                    {
                        reversedByte |= (byte)(1 << (7 - f));
                    }
                }

                bytes[i] = reversedByte;
            }

            File.WriteAllBytes("out.bin", bytes);
        }

        public static void Decompress()
        {
            byte[] fileBytes = File.ReadAllBytes("out.bin");

            for (int i = 0; i < fileBytes.Length; i++)
            {
                byte originalByte = fileBytes[i];
                byte reversedByte = 0;

                for (int f = 0; f < 8; f++)
                {
                    if ((originalByte & (1 << f)) != 0)
                    {
                        reversedByte |= (byte)(1 << (7 - f));
                    }
                }

                fileBytes[i] = reversedByte;
            }

            BitArray bits = new BitArray(fileBytes);
            int N = 0;
            int m = 0;
            long samples = 0;
            int frequency = 0;
            int positivity = 0;
            GetHeader(bits, ref N, ref m, ref samples, ref frequency);
            List<double> inputList = new List<double>();

            for (int i = 96; i + 6 < bits.Length;)
            {
                int bitsCount = 0;
                int bitRep = 0;
                for (int j = 5; j >= 0; j--)
                {
                    if (bits[i]) SetBit(ref bitsCount, j);
                    i++;
                }

                if (bitsCount == 0)
                {
                    break;
                }

                if (bits[i])
                {
                    positivity = -1;
                }
                i++;

                for (int j = bitsCount - 2; j >= 0; j--)
                {
                    if (bits[i]) SetBit(ref bitRep, j);
                    i++;
                }

                inputList.Add(bitRep * positivity);
            }

            Console.WriteLine("stage 00");
            List<double> tmp = new List<double>();
            foreach (double item in inputList)
            {
                tmp.Add(item);
                for (int j = 0; j < m; j++)
                {
                    tmp.Add(0);
                }
            }

            inputList.Clear();
            inputList = tmp.ToList();
            tmp.Clear();
            int blockSize = 2 * N;
            double res = 0;
            List<double> Y = new List<double>();

            Console.WriteLine("stage 11");
            for (int i = 0; i < inputList.Count(); i += N)
            {
                tmp = inputList.Skip(i).Take(N).ToList();
                for (int n = 0; n < blockSize; n++)
                {
                    res = 0;
                    for (int k = 0; k < N; k++)
                    {
                        res += tmp[k] * Math.Cos((Math.PI / (double)N) * ((double)n + 0.5 + ((double)N / 2)) * ((double)k + 0.5));
                    }
                    res = res * (2.0 / (double)N);
                    Y.Add(res);
                }
            }

            Console.WriteLine("stage 22");
            int counter = 0;
            for (int i = 0; i < Y.Count(); i++)
            {
                Y[i] *= Math.Sin((Math.PI / blockSize) * (counter + 0.5));
                counter++;
                if (counter == blockSize)
                    counter = 0;
            }

            Y.RemoveRange(0, N);
            Y.RemoveRange(Y.Count() - N, N);

            List<double> X = new List<double>();
            tmp.Clear();

            Console.WriteLine("stage 33");
            for (int i = 0; i < Y.Count(); i += blockSize)
            {
                tmp = Y.Skip(i).Take(blockSize).ToList();
                res = 0;
                for (int z = 0; z < N; z++)
                {
                    res = tmp[z] + tmp[z + N];
                    X.Add(Math.Round(res));
                }
            }

            List<double> L = new List<double>();
            List<double> R = new List<double>();
            int delimit = X.Count() / 2;

            Console.WriteLine("stage 44");
            for (int i = 0; i < delimit; i++)
            {
                L.Add(X[i] + X[i + delimit]);
                R.Add(X[i] - X[i + delimit]);
            }

            Console.WriteLine("stage 55");
            short[] output = new short[L.Count() * 2];
            WaveFileWriter writer = new WaveFileWriter("out.wav", new WaveFormat(frequency, 16, 2));
            writer.WriteSamples(output, 0, output.Length);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Input your N:");
            int n = 0;
            int.TryParse(Console.ReadLine(), out n);

            Console.WriteLine("Input your M:");
            int m = 0;
            int.TryParse(Console.ReadLine(), out m);

            Compress(n, m);
            Decompress();
            /*Console.WriteLine(CalculateBits(-2)); // Output: 2
            string binary = Convert.ToString(-2, 2);
            Console.WriteLine(binary);*/
        }
    }
}