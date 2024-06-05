using System.Collections;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace ToyDct;

class Program
{
    class Pixel<T>
    {
        public T Color { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int BlockRelativeX { get; set; }
        public int BlockRelativeY { get; set; }
    }
    
    class Block<T> : IEnumerable
    {
        public Pixel<T>[,] Pixels { get; set; } = new Pixel<T>[8,8];
        public int X { get; set; }
        public int Y { get; set; }
        
        public IEnumerator GetEnumerator()
        {
            return Pixels.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    class YCbCr2
    {
        public float Y { get; set; }
        public float Cb { get; set; }
        public float Cr { get; set; }

        public YCbCr2(float y, float cb, float cr)
        {
            Y = y;
            Cb = cb;
            Cr = cr;
        }
    }

    private static Image<Rgb24> img;
    private static Buffer2D<Rgb24> buffer;
    private static List<Block<Rgb24>> blocksRGB = [];
    private static List<Block<YCbCr2>> blocksYCBCR = [];

    private static readonly int[,] quantizationTableLuma = {
        {16, 11, 10, 16, 24, 40, 51, 61},
        {12, 12, 14, 19, 26, 58, 60, 55},
        {14, 13, 16, 24, 40, 57, 69, 56},
        {14, 17, 22, 29, 51, 87, 80, 62},
        {18, 22, 37, 56, 68, 109, 103, 77},
        {24, 35, 55, 64, 81, 104, 113, 92},
        {49, 64, 78, 87, 103, 121, 120, 101},
        {72, 92, 95, 98, 112, 100, 103, 99}
    };
    
    private static readonly int[,] quantizationTableChroma = {
        {17, 18, 24, 47, 99, 99, 99, 99},
        {18, 21, 26, 19, 66, 99, 99, 99},
        {24, 26, 56, 99, 99, 99, 99, 99},
        {47, 66, 99, 99, 99, 99, 99, 99},
        {99, 99, 99, 99, 99, 99, 99, 99},
        {99, 99, 99, 99, 99, 99, 99, 99},
        {99, 99, 99, 99, 99, 99, 99, 99},
        {99, 99, 99, 99, 99, 99, 99, 99}
    };
    
    static async Task Main(string[] args)
    {
        /*List<float> dct1 = DCT([-76, -73, -67, -62, -58, -67, -64, -55]);

        foreach (float d in dct1)
        {
            Console.WriteLine(d);
        }
        
        Console.WriteLine("----------------");

        List<float> originalValues = IDCT(dct1);
        
        foreach (float d in originalValues)
        {
            Console.WriteLine(d);
        }
        
        return;*/
        
        img = await Image.LoadAsync<Rgb24>("lisa.jpg");
        buffer = img.Frames[0].PixelBuffer;

        // encoder
        // ----------------------------------------
        await LoadBlocks();
        
        ToYCBCR(); // 1. get luminescence, chrominance
        await SaveYCBCR("1");
        // 2. chroma downsampling, todo
        CenterBlocks(true); // 3. Subtract 128, so channels are in [-128, 128] range
        BlocksApply(DCT); // 4. DCT
        
        // quantizer
        Quantize();
        //DiscardHighFrequencyBlocks();
        
        // decoder
        // ---------------------------------------
        
        // 4. test: IDCT
        BlocksApply(IDCT);
        CenterBlocks(false);
        
        await SaveYCBCR("2");

        //await SaveYCBCR();
    }

    static void Quantize()
    {
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            foreach (Pixel<YCbCr2> pixel in block)
            {
                int y = (int)Math.Round(pixel.Color.Y / quantizationTableLuma[pixel.BlockRelativeX, pixel.BlockRelativeY]);

                if (y is 0)
                {
                    pixel.Color.Y = 0;
                }
            }
        }
    }

    static void DiscardHighFrequencyBlocks()
    {
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            List<Pixel<YCbCr2>> pixelsList = block.Pixels.Cast<Pixel<YCbCr2>>().ToList();
            pixelsList = pixelsList.OrderByDescending(x => Math.Abs(x.Color.Y)).ToList();
            
            // keep all but the few most important
            foreach (Pixel<YCbCr2> pixel in pixelsList.Skip(10))
            {
                pixel.Color.Y = 0;
            }
        }
    }

    static void PrintMinMaxValues()
    {
        float min = 999;
        float max = 0;
        
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            foreach (Pixel<YCbCr2> pixel in block)
            {
                if (pixel.Color.Y > max)
                {
                    max = pixel.Color.Y;
                }

                if (pixel.Color.Y < min)
                {
                    min = pixel.Color.Y;
                }
            }
        }
        
        Console.WriteLine(max);
        Console.WriteLine(min);
    }

    static void CenterBlocks(bool subtract)
    {
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            foreach (Pixel<YCbCr2> pixel in block)
            {
                pixel.Color = new YCbCr2(pixel.Color.Y + (subtract ? -128 : 128), pixel.Color.Cb, pixel.Color.Cr);
            }
        }
    }

    static List<float> DCT(List<float> nums)
    {
        List<float> ret = [];
        
        for (int i = 0; i < nums.Count; i++)
        {
            double sum = nums.Select((t, j) => t * Math.Cos((Math.PI / nums.Count) * (j + 0.5d) * i)).Sum();

            ret.Add((float)sum);
        }

        return ret;
    }
    
    static List<float> IDCT(List<float> nums)
    {
        List<float> ret = [];
        
        for (int i = 0; i < nums.Count; i++)
        {
            double sum = 0;

            for (int j = 1; j < nums.Count - 1; j++)
            {
                sum += nums[j] * Math.Cos((Math.PI / nums.Count) * (i + 0.5d) * j);
            }

            double fn = (2f / nums.Count) * (0.5f * nums[0] + sum);

            ret.Add((float)fn);
        }

        return ret;
    }

    static void BlocksApply(Func<List<float>, List<float>> fn)
    {
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            // 1. 1D DCT over each row
            for (int i = 0; i < 8; i++)
            {
                List<float> rowPixels = [];
                
                for (int j = 0; j < 8; j++)
                {
                    rowPixels.Add(block.Pixels[j, i].Color.Y);
                }

                rowPixels = fn.Invoke(rowPixels);
                
                for (int j = 0; j < 8; j++)
                {
                    block.Pixels[j, i].Color = new YCbCr2(rowPixels[j], block.Pixels[j, i].Color.Cb, block.Pixels[j, i].Color.Cr);
                }
            }
            
            // 2. then over each column
            for (int i = 0; i < 8; i++)
            {
                List<float> columnPixels = [];
                
                for (int j = 0; j < 8; j++)
                {
                    columnPixels.Add(block.Pixels[i, j].Color.Y);
                }

                columnPixels = fn.Invoke(columnPixels);
                
                for (int j = 0; j < 8; j++)
                {
                    block.Pixels[i, j].Color = new YCbCr2(columnPixels[j], block.Pixels[i, j].Color.Cb, block.Pixels[i, j].Color.Cr);
                }
            }
        }
    }

    static void PrintY()
    {
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            foreach (Pixel<YCbCr2> pixel in block)
            {
                Console.WriteLine(pixel.Color.Y);
            }
            
            Console.WriteLine("---------");
        }
    }

    static async Task SaveYCBCR(string phase)
    {
        List<Block<Rgb24>> convertedBlocks = [];

        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            Block<Rgb24> convertedBlock = new Block<Rgb24>
            {
                X = block.X,
                Y = block.Y
            };

            foreach (Pixel<YCbCr2> pixel in block)
            {
                double r = pixel.Color.Y + 1.402d * (pixel.Color.Cr - 128);
                double g = pixel.Color.Y - 0.34414d * (pixel.Color.Cb - 128) - 0.71414d * (pixel.Color.Cr - 128);
                double b = pixel.Color.Y + 1.772d * (pixel.Color.Cb - 128);
                
                convertedBlock.Pixels[pixel.BlockRelativeX, pixel.BlockRelativeY] = new Pixel<Rgb24>
                {
                    X = pixel.X,
                    Y = pixel.Y,
                    BlockRelativeX = pixel.BlockRelativeX,
                    BlockRelativeY = pixel.BlockRelativeY,
                    Color = new Rgb24((byte)r, (byte)g, (byte)b)
                };
            }
            
            convertedBlocks.Add(convertedBlock);
        }
        
        // y
        Image<Rgb24> img = CreateEmptyImg();

        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            foreach (Pixel<YCbCr2> pixel in block)
            {
                img[pixel.X, pixel.Y] = new Rgb24((byte)pixel.Color.Y, (byte)pixel.Color.Y, (byte)pixel.Color.Y);
            }
        } 
        
        await Save(img, $"mask_y_{phase}");
        
        // cb
        img = CreateEmptyImg();
        
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            foreach (Pixel<YCbCr2> pixel in block)
            {
                img[pixel.X, pixel.Y] = new Rgb24(255, 255, (byte)pixel.Color.Cb);
            }
        } 
        
        await Save(img, $"mask_cb_{phase}");
        
        // cr
        img = CreateEmptyImg();
        
        foreach (Block<YCbCr2> block in blocksYCBCR)
        {
            foreach (Pixel<YCbCr2> pixel in block)
            {
                img[pixel.X, pixel.Y] = new Rgb24((byte)pixel.Color.Cr, 255, 255);
            }
        } 
        
        // rgb
        img = CreateEmptyImg();
        
        foreach (Block<Rgb24> block in convertedBlocks)
        {
            foreach (Pixel<Rgb24> pixel in block)
            {
                img[pixel.X, pixel.Y] = new Rgb24(pixel.Color.R, pixel.Color.G, pixel.Color.B);
            }
        } 
        
        await Save(img, $"mask_rgb_{phase}");
    }
    
    static void ToYCBCR()
    {
        foreach (Block<Rgb24> block in blocksRGB)
        {
            Block<YCbCr2> transformedBlock = new Block<YCbCr2>
            {
                X = block.X,
                Y = block.Y
            };

            foreach (Pixel<Rgb24> pixel in block)
            {
                double y = 0.299d * pixel.Color.R + 0.587 * pixel.Color.G + 0.114 * pixel.Color.B;
                double cb = 128 - 0.168736d * pixel.Color.R - 0.331364d * pixel.Color.G + 0.5d * pixel.Color.B;
                double cr = 128 + 0.5 * pixel.Color.R - 0.418688d * pixel.Color.G - 0.081312 * pixel.Color.B;
                
                transformedBlock.Pixels[pixel.BlockRelativeX, pixel.BlockRelativeY] = new Pixel<YCbCr2>
                {
                    X = pixel.X,
                    Y = pixel.Y,
                    Color = new YCbCr2((float)y, (float)cb, (float)cr),
                    BlockRelativeX = pixel.BlockRelativeX,
                    BlockRelativeY = pixel.BlockRelativeY
                };
            }
            
            blocksYCBCR.Add(transformedBlock);
        }
    }

    static async Task LoadBlocks()
    {
        // if either axis is not divisible by 8, then the axis (right / bottom), should be padded with 0 to be 8 divisible
        int x = 0, y = 0;
        
        for (int i = 0; i < img.Height; i += 8)
        {
            for (int j = 0; j < img.Width; j += 8)
            {
                Block<Rgb24> block = new Block<Rgb24>
                {
                    X = x,
                    Y = y
                };

                for (int k = 0; k < 8; k++)
                {
                    for (int l = 0; l < 8; l++)
                    {
                        block.Pixels[k, l] = new Pixel<Rgb24>
                        {
                            Color = buffer[j + l, i + k],
                            X = j + l,
                            Y = i + k,
                            BlockRelativeX = k,
                            BlockRelativeY = l
                        };
                    }
                }

                blocksRGB.Add(block);
                x++;
            }

            y++;
            x = 0;
        }
    }

    private static string BasePath => Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.Parent?.Parent?.Parent?.FullName ?? string.Empty;

    static async Task Save(Image<Rgb24> image, string nameWithoutExtension)
    {
        await image.SaveAsJpegAsync($"{BasePath}\\{nameWithoutExtension}.jpg");
    }

    static Image<Rgb24> CreateEmptyImg()
    {
        return new Image<Rgb24>(img.Width, img.Height);
    }

    static async Task DumpEveryOtherBlock()
    {
        Image<Rgb24> testImg = new Image<Rgb24>(img.Width, img.Height);
        bool b = true;
        Block<Rgb24> prevBlock = blocksRGB[0];
        
        foreach (Block<Rgb24> block in blocksRGB)
        {
            if (b)
            {
                foreach (Pixel<Rgb24> pixel in block.Pixels)
                {
                    testImg[pixel.X, pixel.Y] = pixel.Color;
                }
            }
            
            b = !b;

            if (block.X is 31)
            {
                b = !b;
            }
            
            prevBlock = block;
        }


        await testImg.SaveAsJpegAsync($"{BasePath}\\lisa_every_other_block.jpg");
    }
}