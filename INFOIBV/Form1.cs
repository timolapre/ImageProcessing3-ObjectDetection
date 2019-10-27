using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace INFOIBV
{
    public partial class INFOIBV : Form
    {
        private Bitmap InputImage;
        private Bitmap OutputImage;
        private Bitmap houghImageBitmap;
        private int OutputWidth, OutputHeight;
        float[,] houghImage;

        private int startX = -1, startY = -1, firstX = -1, firstY = -1;
        private int boundaryX, boundaryY, boundaryDirection;

        private int houghSize = 500;
        int maxAngle, minAngle;

        public INFOIBV()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
        }

        //Load image
        private void LoadImageButton_Click(object sender, EventArgs e)
        {
            if (openImageDialog.ShowDialog() == DialogResult.OK)             // Open File Dialog
            {
                string file = openImageDialog.FileName;                     // Get the file name
                imageFileName.Text = file;                                  // Show file name
                if (InputImage != null) InputImage.Dispose();               // Reset image
                InputImage = new Bitmap(file);                              // Create new Bitmap from file
                if (InputImage.Size.Height <= 0 || InputImage.Size.Width <= 0 ||
                    InputImage.Size.Height > 512 || InputImage.Size.Width > 512) // Dimension check
                    MessageBox.Show("Error in image dimensions (have to be > 0 and <= 512)");
                else
                    pictureBox1.Image = (Image)InputImage;                 // Display input image
            }
        }

        //Do something with the image
        private void applyButton_Click(object sender, EventArgs e)
        {
            if (InputImage == null) return;                                 // Get out if no input image
            if (OutputImage != null) OutputImage.Dispose();                 // Reset output image
            OutputImage = new Bitmap(InputImage.Size.Width, InputImage.Size.Height); // Create new output image
            if (houghImageBitmap != null) houghImageBitmap.Dispose();
            Color[,] Image = new Color[InputImage.Size.Width, InputImage.Size.Height]; // Create array to speed-up operations (Bitmap functions are very slow)

            // Setup progress bar
            progressBar.Visible = true;
            progressBar.Minimum = 1;
            progressBar.Maximum = (InputImage.Size.Width * InputImage.Size.Height) * 2;
            progressBar.Value = 1;
            progressBar.Step = 1;

            // Copy input Bitmap to array            
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    Image[x, y] = InputImage.GetPixel(x, y);                // Set pixel color in array at (x,y)
                    progressBar.PerformStep();
                }
            }

            //==========================================================================================
            // TODO: include here your own code
            // example: create a negative image

            OutputWidth = InputImage.Size.Width;
            OutputHeight = InputImage.Size.Height;

            int[,] grayscaleImage = new int[InputImage.Size.Width, InputImage.Size.Height];
            for (int i = 0; i < InputImage.Size.Width; i++)
            {
                for (int j = 0; j < InputImage.Size.Height; j++)
                {
                    grayscaleImage[i, j] = toGrayscale(Image[i, j]);
                }
            }

            if (ContrastAdjustmentCheckbox.Checked)
            {
                grayscaleImage = fullRangeContrastImage(grayscaleImage);
            }

            if (ComplementCheckbox.Checked)
            {
                complement(grayscaleImage);
            }

            if (EdgDetection.Checked)
            {
                int[,] ImageX = applyKernel(grayscaleImage, createEdgeKernel(true));
                int[,] ImageY = applyKernel(grayscaleImage, createEdgeKernel(false));
                for (int i = 0; i < Image.GetLength(0); i++)
                {
                    for (int j = 0; j < Image.GetLength(1); j++)
                    {
                        //Debug.WriteLine(ImageX[i, j]);
                        int color = (int)Math.Sqrt(Math.Pow(ImageX[i, j], 2) + Math.Pow(ImageY[i, j], 2));
                        grayscaleImage[i, j] = color;
                    }
                }
            }

            if (BoundaryTrace.Checked)
            {
                getBoundary(grayscaleImage);
            }

            if (houghTransformCheckbox.Checked)
            {
                float[,] acc = getHoughTransform(grayscaleImage, houghImage);
                for (int x = 0; x < 180; x++)
                {
                    for (int y = 0; y < 212; y++)
                    {
                        houghImage[x, y] = acc[x, y];
                    }
                }
                for (int i = 0; i < 180; i++)
                {
                    for (int j = 0; j < 212; j++)
                    {
                        int val = truncate((int)houghImage[i, j]);
                        Color color = Color.FromArgb(val, val, val);
                        houghImageBitmap.SetPixel(i, j, color);
                    }
                }
                houghImageOutput.Image = houghImageBitmap;
            }

            //truncate and return grayscale image to actual image
            for (int i = 0; i < OutputWidth; i++)
            {
                for (int j = 0; j < OutputHeight; j++)
                {
                    int color = truncate(grayscaleImage[i, j]);
                    Image[i, j] = Color.FromArgb(color, color, color);
                    progressBar.PerformStep();
                }
            }

            // linedetection
            int[,] lineimage = new int[InputImage.Width, InputImage.Height]; // will contain all the lines without the original picture in it. later used to detect dice.
            if (lineDetectionCheckbox.Checked)
            {
                float[,] acc = Accumulator(grayscaleImage, int.Parse(houghThresholdVal.Text));
                int rmax = (int)Math.Sqrt(Math.Pow(OutputHeight, 2) + Math.Pow(OutputWidth, 2));
                houghImage = new float[rmax * 2 + 1, rmax * 2 + 1];
                houghImageBitmap = new Bitmap(180, rmax * 2);
                houghImageOutput.Image = houghImageBitmap;

                for (int x = 0; x < 180; x++)
                {
                    for (int y = 0; y < rmax * 2; y++)
                    {
                        houghImage[x, y] = acc[x, y];
                        int val = truncate((int)houghImage[x, y]);
                        Color color = Color.FromArgb(val, val, val);
                        houghImageBitmap.SetPixel(x, y, color);
                    }
                }
                houghImageOutput.Image = houghImageBitmap;
                for (int o = 0; o <= 180; o++)
                {
                    for (int r = 0; r < rmax * 2; r++)
                    {
                        if (acc[o, r] == 255)
                        {
                            LineDetection(ref Image, ref lineimage ,r - rmax, toRadian(o), int.Parse(minIntensityThresVal.Text), int.Parse(minLengthParVal.Text), int.Parse(maxGapParVal.Text));
                        }
                    }
                }
            }

            List<Circle> possibledice = new List<Circle>();
            int[,] circleimage = new int[InputImage.Width, InputImage.Height];// will contain only the circles that will be detected
            // circle detection
            if (circleDetection.Checked)
            {
                int[,,] accumulator = CircleAccumulator(grayscaleImage, int.Parse(CircleThreshold.Text));
                for (int a = 0; a < InputImage.Width; a++)
                {
                    for (int b = 0; b < InputImage.Height; b++)
                    {
                        for (int r = 0; r < accumulator.GetLength(2); r++)
                        {
                            if (accumulator[a, b, r] > 0)
                            {
                                possibledice.Add(new Circle(r,a,b));
                                DrawCircle(ref Image, ref circleimage, a, b, r, Color.FromArgb(255,0,0));
                            }
                        }
                    }
                }
            }

            // dice detection
            List<Circle> pos2 = SearchForLine(possibledice, lineimage);
            foreach (Circle c in pos2)
            {
                DrawCircle(ref Image, ref circleimage, c.a, c.b, c.r, Color.FromArgb(0,0,255));
            }
            SearchForCircleRow(pos2, circleimage, Image);

            //==========================================================================================

            // Copy array to output Bitmap
            for (int x = 0; x < OutputWidth - 1; x++)
            {
                for (int y = 0; y < OutputHeight - 1; y++)
                {
                    OutputImage.SetPixel(x, y, Image[x, y]);               // Set the pixel color at coordinate (x,y)
                }
            }

            pictureBox2.Image = (Image)OutputImage;                         // Display output image
            progressBar.Visible = false;                                    // Hide progress bar
        }




        // \/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/ --- OUR FUNCTIONS --- \/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/ \\

        //Or actually display hough values in an image
        private float[,] getHoughTransform(int[,] img, float[,] houghImage)
        {
            maxAngle = (int)((int.Parse(houghAngleMaxValue.Text) + 1) * Math.PI / 180f);
            minAngle = (int)(int.Parse(houghAngleMinValue.Text) * Math.PI / 180f);
            int m = (int)Math.Sqrt(Math.Pow(OutputHeight, 2) + Math.Pow(OutputWidth, 2));
            float[,] accum = new float[int.Parse(houghAngleMaxValue.Text)+1, m];
            float l = 0.78539791f;
            float max = (float)(m * Math.Cos(l) + m * Math.Sin(l));
            float min = -max;
            //a = houghSize / (max - min);
            //b = -(a * min);
            for (int y = 0; y < OutputHeight - 1; y++)
            {
                Console.WriteLine(y);
                for (int x = 0; x < OutputWidth - 1; x++)
                {
                    for (int o = 0; o <= int.Parse(houghAngleMaxValue.Text); o += 1)
                    {
                        //Debug.WriteLine(o*180/Math.PI);
                        float r = Math.Abs((float)((x - OutputWidth / 2) * Math.Cos(o) + (y - OutputHeight / 2) * Math.Sin(o)));
                        //Debug.WriteLine(max - min + " " + a + " " + b + " " + r + " " + a*r+b);
                        accum[o, (int)r] += 0.3f;
                        //houghImage[(int)(Math.Round(o * houghSize / maxAngle)), (int)(Math.Round(a * r + b))] += img[x, y] * 0.4f / 255f;
                    }
                }
            }
            return accum;
        }

        //LineDetection Tim
        private void LineDetection(ref Color[,] img, ref int[,] lineimage,double r, double o, int minThreshold, int minLength, int maxGap)
        {
            int[,] lines = new int[img.GetLength(0),img.GetLength(1)];
            double posX = Math.Cos(o) * r;
            double posY = Math.Sin(o) * r;
            o = (o + toRadian(90))%toRadian(180);
            double dx = Math.Cos(o);
            double dy = Math.Sin(o);
            //Debug.WriteLine(toDegree(o) + " " + o + " " + r + " " + posX + " " + posY + " " + dx + " " + dy);
            //Debug.WriteLine(Math.Cos(o) + " " + posX / Math.Cos(o));
            int rmax = (int)Math.Sqrt(Math.Pow(OutputHeight, 2) + Math.Pow(OutputWidth, 2));
            for (int i = -rmax; i < rmax; i++)
            {
                int x = (int)Math.Round(posX + dx * i);
                int y = (int)Math.Round(posY + dy * i);
                if (x < 0 || y < 0 || x >= InputImage.Width || y >= InputImage.Height) continue;
                img[x, y] = Color.FromArgb(0, 255, 0);
                lineimage[x, y] = 255;
            }
        }

        //searchforLine, deletes all circles from possibledice that don't have a line close to them.
        private List<Circle> SearchForLine(List<Circle> circles, int[,] lines)
        {
            List<Circle> output = new List<Circle>();
            foreach (Circle c in circles)
            {
                bool found = false;
                for (int i = c.r; i < c.r * 3; i++)
                {
                    for (int o = 0; o < 360; o+=2)
                    {
                        int x = (int)(Math.Cos(o) * i + c.a);
                        int y = (int)(Math.Sin(o) * i + c.b);
                        if (x < 0 || x >= InputImage.Width || y < 0 || y >= InputImage.Height) continue;
                        if (lines[x,y] == 255)
                        {
                            found = true;
                            output.Add(c);
                            break;
                        }
                    }
                    if (found) break;
                }
                if (found) continue;
            }
            return output;
        }

        //search for circles that lay on one line (possible on a die)
        private void SearchForCircleRow(List<Circle> circles, int[,] circleimage, Color[,] img)
        {
            List<Circle> output = new List<Circle>();
            foreach (Circle c in circles)
            {
                for (int i = 0; i < 360; i+=2)
                {
                    int found = 0;
                    for (int r = c.r + 1; r < c.r * 5; r++)
                    {
                        int x = (int)(c.a + Math.Cos(toRadian(i)) * r);
                        int y = (int)(c.b + Math.Sin(toRadian(i)) * r);
                        if (x < 0 || x >= InputImage.Width || y < 0 || y >= InputImage.Height) continue;
                        if (circleimage[x,y] == 255)
                        {
                            found += 1;
                        }
                    }
                    if (found > 8)
                    {
                        DrawLine(c, i, ref img);
                    }
                }
            }
        }

        //Option B. Basically thresholding hough image
        private float[,] NonMaxSuppression(float[,] houghImage)
        {
            for (int x = 0; x < houghSize - 1; x++)
            {
                for (int y = 0; y < houghSize - 1; y++)
                {
                    int val = truncate((int)houghImage[x, y]);
                    if (houghThresholdCheckbox.Checked)
                    {
                        int up = truncate((int)houghImage[Math.Max(Math.Min(x, houghSize - 1), 0), Math.Max(Math.Min(y + 1, houghSize - 1), 0)]);
                        int down = truncate((int)houghImage[Math.Max(Math.Min(x, houghSize - 1), 0), Math.Max(Math.Min(y - 1, houghSize - 1), 0)]);
                        int left = truncate((int)houghImage[Math.Max(Math.Min(x - 1, houghSize - 1), 0), Math.Max(Math.Min(y, houghSize - 1), 0)]);
                        int right = truncate((int)houghImage[Math.Max(Math.Min(x + 1, houghSize - 1), 0), Math.Max(Math.Min(y, houghSize - 1), 0)]);
                        if (up > val || down > val || right > val || left > val)
                        {
                            val = 0;
                        }
                        if (val < int.Parse(houghThresholdVal.Text))
                            val = 0;
                        else
                            val = 255;
                        houghImage[x, y] = val;
                    }
                    Color color = Color.FromArgb(val, val, val);
                    houghImageBitmap.SetPixel(x, y, color);
                }
            }
            return houghImage;
        }

        private void DrawCircle(ref Color[,] img, ref int[,] circleimage,int a, int b, int r, Color color)
        {
            for (int i = 0; i < 360; i++)
            {
                int x = (int)(Math.Cos(toRadian(i)) * r + a);
                int y = (int)(Math.Sin(toRadian(i)) * r + b);
                if (x < 0 || x >= InputImage.Width || y < 0 || y >= InputImage.Height) continue;
                circleimage[x, y] = 255;
                img[x, y] = color;
            }
        }

        private void DrawLine(Circle c, int o,ref Color[,] img)
        {
            int rmax = c.r * 5;
            for (int i = -rmax; i < rmax; i++)
            {
                int x = (int)(c.a + Math.Cos(toRadian(o)) * i);
                int y = (int)(c.b + Math.Sin(toRadian(o)) * i);
                if (x < 0 || x >= InputImage.Width || y < 0 || y >= InputImage.Height) continue;
                img[x, y] = Color.FromArgb(0, 0, 255);
            }
        }

        // hough circle detection
        private int[,,] CircleAccumulator(int[,] img, int threshold)
        {
            int rmin = 5; // minimum radius of a circle to be deteced
            int rmax = 100; // maximum radius of a circle to be detected
            int[,,] acc = new int[InputImage.Width, InputImage.Height, rmax];
            for (int x = 0; x < InputImage.Width; x++)
            {
                for (int y = 0; y < InputImage.Height; y++)
                {
                    if (img[x, y] < 255) continue;
                    for (int r = rmin; r < rmax; r++)
                    {
                        for (int t = 0; t < 360; t +=2)
                        {
                            int a = (int)(x - r * Math.Cos(t * Math.PI / 180));
                            int b = (int)(y - r * Math.Sin(t * Math.PI / 180));
                            if (a < 0 || a >= InputImage.Width) continue;
                            if (b < 0 || b >= InputImage.Height) continue;
                            acc[a, b, r] += 1;
                        }
                    }
                }
            }
            Debug.WriteLine("start supression");
            // 3d non-max suppression
            for (int x = 0; x < InputImage.Width; x++)
            {
                for (int y = 0; y < InputImage.Height; y++)
                {
                    for (int r = rmin; r < rmax; r++)
                    {
                        int value = acc[x, y, r];
                        if (value < threshold)
                        {
                            acc[x, y, r] = 0;
                            continue;
                        }

                        int n1 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r), 0)];
                        int n2 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r), 0)];
                        int n3 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r), 0)];
                        int n4 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r), 0)];
                        int n5 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r), 0)];
                        int n6 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r), 0)];
                        int n7 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r), 0)];
                        int n8 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r), 0)];

                        int n9 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n10 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n11 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n12 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n13 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n14 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n15 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n16 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r - 1), 0)];
                        int n17 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r - 1), 0)];

                        int n18 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n19 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n20 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y + 1), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n21 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n22 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n23 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n24 = acc[Math.Max(Math.Min(InputImage.Width, x - 1), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n25 = acc[Math.Max(Math.Min(InputImage.Width, x), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r + 1), 0)];
                        int n26 = acc[Math.Max(Math.Min(InputImage.Width, x + 1), 0), Math.Max(Math.Min(InputImage.Height, y - 1), 0), Math.Max(Math.Min(359, r + 1), 0)];

                        if (n1 > value || n2 > value || n3 > value || n4 > value || n5 > value || n6 > value || n7 > value || n8 > value || n9 > value || n10 > value || n11 > value || n12 > value || n13 > value || n14 > value || n15 > value || n16 > value || n17 > value || n18 > value || n19 > value || n20 > value || n21 > value || n22 > value || n23 > value || n24 > value || n25 > value || n26 > value)
                        {
                            acc[x, y, r] = 0;
                        }
                    }
                }
            }
            return acc;
        }

        // returns (theta, r) pairs, stored in a 2d array
        private float[,] Accumulator(int[,] img, int threshold)
        {
            int rmax = (int)Math.Sqrt(Math.Pow(OutputHeight, 2) + Math.Pow(OutputWidth, 2));
            float[,] acc = new float[180 + 1, rmax*2 + 1];

            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    if (img[x, y] > threshold)
                    {
                        for (int o = 0; o <= 180; o++)
                        {
                            double r = x * Math.Cos(toRadian(o)) + y * Math.Sin(toRadian(o));
                            //Debug.WriteLine(r);
                            acc[o, (int)(r+rmax)] += 1;
                        }
                    }
                }
            }
            
            // non-maximum suppression
            for (int x = 0; x < 180; x++)
            {
                for (int y = 0; y < rmax*2; y++)
                {
                    float value = acc[x, y];
                    float up = acc[Math.Max(Math.Min(180, x), 0), Math.Max(Math.Min(rmax*2, (y + 1)), 0)];
                    float right = acc[Math.Max(Math.Min(180, (x + 1)), 0), Math.Max(Math.Min(rmax*2, y), 0)];
                    float down = acc[Math.Max(Math.Min(180, (x)), 0), Math.Max(Math.Min(rmax*2, (y - 1)), 0)];
                    float left = acc[Math.Max(Math.Min(180, (x - 1)), 0), Math.Max(Math.Min(rmax*2, y), 0)];
                    if (up > value || down > value || right > value || left > value)
                    {
                        value = 0;
                    }
                    if (value < threshold)
                    {
                        value = 0;
                    }
                    else
                    {
                        value = 255;
                    }
                    acc[x, y] = value;
                }
            }
            return acc;
        }




        // \/\/\/\/  USEFUL FILTER FUNCTIONS WE DONT NEED TO TOUCH (PROBABLY)  \/\/\/\/
        private int[,] complement(int[,] img)
        {
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    int pixelColor = img[x, y];
                    img[x, y] = 255 - pixelColor;
                }
            }
            return img;
        }

        private int[,] applyKernel(int[,] img, float[,] kernel)
        {
            int[,] ImageWithkernel = new int[InputImage.Size.Width, InputImage.Size.Height];
            int size = ((int)Math.Sqrt(kernel.Length) - 1) / 2;
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    float value = 0;
                    if (x + size >= InputImage.Size.Width || x - size < 0 || y + size >= InputImage.Size.Height || y - size < 0)
                        continue;
                    for (int n = -size; n <= size; n++)
                    {
                        for (int m = -size; m <= size; m++)
                        {
                            value += kernel[n + size, m + size] * img[x + n, y + m];
                        }
                    }
                    ImageWithkernel[x, y] = (int)value;
                }
            }
            return ImageWithkernel;
        }

        private int[,] fullRangeContrastImage(int[,] img)
        {
            int[,] ImageWithkernel = new int[InputImage.Size.Width, InputImage.Size.Height];
            float min = 255;
            float max = 0;
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    if (img[x, y] < min)
                        min = img[x, y];
                    if (img[x, y] > max)
                        max = img[x, y];
                }
            }

            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    ImageWithkernel[x, y] = (int)((img[x, y] - min) / (max - min) * 255);
                }
            }

            return ImageWithkernel;
        }

        private int[,] edgeDetection(int[,] grayscaleImage)
        {
            int[,] returnImg = new int[InputImage.Size.Width, InputImage.Size.Height];
            int[,] ImageX = applyKernel(grayscaleImage, createEdgeKernel(true));
            int[,] ImageY = applyKernel(grayscaleImage, createEdgeKernel(false));
            for (int i = 0; i < OutputWidth; i++)
            {
                for (int j = 0; j < OutputHeight; j++)
                {
                    //Debug.WriteLine(ImageX[i, j]);
                    int color = (int)Math.Sqrt(Math.Pow(ImageX[i, j], 2) + Math.Pow(ImageY[i, j], 2));
                    returnImg[i, j] = color;
                }
            }
            return returnImg;
        }

        private float[,] createEdgeKernel(bool x = true)
        {
            float[,] kernel = new float[,] { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            if (!x)
            {
                kernel = new float[,] { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };
            }
            return kernel;
        }

        private void BiggestShape_CheckedChanged(object sender, EventArgs e)
        {
            if (BiggestShape.Checked)
                BoundaryTrace.Checked = true;
        }

        private void FullShapes_CheckedChanged(object sender, EventArgs e)
        {
            if (FullShapes.Checked)
                BoundaryTrace.Checked = true;
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (OutputImage == null) return;                                // Get out if no output image
            if (saveImageDialog.ShowDialog() == DialogResult.OK)
                OutputImage.Save(saveImageDialog.FileName);                 // Save the output image
        }

        private void houghTransformCheckbox_CheckedChanged(object sender, EventArgs e)
        {
        }

        private int toGrayscale(Color pixelColor)
        {
            int grayscale = (int)((pixelColor.R * 0.3f) + (pixelColor.G * 0.59f) + (pixelColor.B * 0.11f));
            return grayscale;
        }





        // \/\/\/\/  SOME EASY USEFUL MATH FUNCTIONS  \/\/\/\/
        private double toRadian(double o)
        {
            return o * Math.PI / 180;
        }

        private double toDegree(double o)
        {
            return o * 180/ Math.PI;
        }

        private int truncate(int value)
        {
            return Math.Max(Math.Min(value, 255), 0);
        }





        // \/\/\/\/  THINGS WE MOST CERTAINLY PROBABLY DONT NEED I THINK  \/\/\/\/
        private void getBoundary(int[,] img)
        {
            List<int> shapeSizeList = new List<int>();
            List<List<int[]>> shapeBoundaries = new List<List<int[]>>();
            int[,] labelImg = new int[InputImage.Size.Width, InputImage.Size.Height];
            getBinaryLabel(img, labelImg);
            int label = 1;
            for (int y = 1; y < InputImage.Size.Height - 1; y++)
            {
                for (int x = 1; x < InputImage.Size.Width - 1; x++)
                {
                    if (labelImg[x - 1, y] == 0 && labelImg[x, y] == 1)
                    {
                        shapeSizeList.Add(0);
                        shapeBoundaries.Add(new List<int[]>());
                        label++;
                        startX = x;
                        startY = y;
                        boundaryX = x;
                        boundaryY = y;
                        boundaryDirection = 0;
                        firstX = -1;
                        firstY = -1;
                        while (traceContour(labelImg, label, true, shapeSizeList, shapeBoundaries)) ;
                    }
                    else if (labelImg[x - 1, y] > 1 && labelImg[x, y] == 0)
                    {
                        startX = x;
                        startY = y;
                        boundaryX = x;
                        boundaryY = y;
                        boundaryDirection = 0;
                        firstX = -1;
                        firstY = -1;
                        while (traceContourInner(labelImg, labelImg[x - 1, y], false, shapeSizeList, shapeBoundaries)) ;
                    }
                    else if (labelImg[x - 1, y] > 1 && labelImg[x, y] == 1)
                    {
                        labelImg[x, y] = labelImg[x - 1, y];
                    }
                }
            }

            int biggestShapeSize = 0;
            int biggestShape = 0;

            if (!FullShapes.Checked)
            {
                for (int x = 0; x < InputImage.Size.Width; x++)
                {
                    for (int y = 0; y < InputImage.Size.Height; y++)
                    {
                        img[x, y] = 0;
                    }
                }
                if (BiggestShape.Checked)
                {
                    for (int i = 0; i < shapeSizeList.Count; i++)
                    {
                        if (shapeSizeList[i] > biggestShapeSize)
                        {
                            biggestShapeSize = shapeSizeList[i];
                            biggestShape = i;
                        }
                    }
                    foreach (int[] pos in shapeBoundaries[biggestShape])
                    {
                        int x = pos[0];
                        int y = pos[1];
                        img[x, y] = 255;
                    }
                }
                else
                {
                    foreach (List<int[]> lists in shapeBoundaries)
                    {
                        foreach (int[] pos in lists)
                        {
                            int x = pos[0];
                            int y = pos[1];
                            img[x, y] = 255;
                        }
                    }
                }
            }
            else
            {
                if (BiggestShape.Checked)
                {
                    for (int i = 0; i < shapeSizeList.Count; i++)
                    {
                        if (shapeSizeList[i] > biggestShapeSize)
                        {
                            biggestShapeSize = shapeSizeList[i];
                            biggestShape = i;
                        }
                    }
                }

                for (int x = 0; x < InputImage.Size.Width; x++)
                {
                    for (int y = 0; y < InputImage.Size.Height; y++)
                    {
                        if (labelImg[x, y] <= 0)
                            img[x, y] = 0;
                        if (BiggestShape.Checked)
                        {
                            if (biggestShape + 2 == labelImg[x, y])
                                img[x, y] = 255;
                            else
                                img[x, y] = 0;
                        }
                        else if (labelImg[x, y] > 1)
                            img[x, y] = 255;// - labelImg[x, y] * 20;
                    }
                }
            }
        }

        private bool traceContour(int[,] labelImg, int label, bool sizeCount, List<int> shapeSize, List<List<int[]>> shapeBoundaries)
        {
            labelPixels(labelImg, boundaryX, boundaryY, label);

            if (sizeCount)
                shapeSize[label - 2]++;
            if (!FullShapes.Checked)
                shapeBoundaries[label - 2].Add(new int[2] { boundaryX, boundaryY });
            //Debug.WriteLine(startX + " " + startY + " " + firstX + " " + firstY + " " + label + " " + boundaryDirection + " " + boundaryX + " " + boundaryY);
            if (firstX == -1 && firstY == -1 && (boundaryX != startX || boundaryY != startY))
            {
                firstX = boundaryX;
                firstY = boundaryY;
            }
            for (int i = boundaryDirection; i < boundaryDirection + 4; i++)
            {
                if (i % 4 == 0)
                {
                    if (boundaryY > 0 && labelImg[boundaryX, boundaryY - 1] >= 1)
                    {
                        if (boundaryX == firstX && boundaryY - 1 == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = (i + 3) % 4;
                        boundaryY = boundaryY - 1;
                        return true;
                    }
                    else
                        continue;
                }
                if (i % 4 == 1)
                {
                    if (boundaryX < OutputWidth - 1 && labelImg[boundaryX + 1, boundaryY] >= 1)
                    {
                        if (boundaryX + 1 == firstX && boundaryY == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = (i + 3) % 4;
                        boundaryX = boundaryX + 1;
                        return true;
                    }
                    else
                        continue;
                }
                if (i % 4 == 2)
                {
                    if (boundaryY < OutputHeight - 1 && labelImg[boundaryX, boundaryY + 1] >= 1)
                    {
                        if (boundaryX == firstX && boundaryY + 1 == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = (i + 3) % 4;
                        boundaryY = boundaryY + 1;
                        return true;
                    }
                    else
                        continue;
                }
                if (i % 4 == 3)
                {
                    if (boundaryX > 0 && labelImg[boundaryX - 1, boundaryY] >= 1)
                    {
                        if (boundaryX - 1 == firstX && boundaryY == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = (i + 3) % 4;
                        boundaryX = boundaryX - 1;
                        return true;
                    }
                    else
                        continue;
                }
            }
            return false;
        }

        private bool traceContourInner(int[,] labelImg, int label, bool sizeCount, List<int> shapeSize, List<List<int[]>> shapeBoundaries)
        {
            labelPixels(labelImg, boundaryX, boundaryY, label);

            if (sizeCount)
                shapeSize[label - 2]++;
            if (!FullShapes.Checked)
                shapeBoundaries[label - 2].Add(new int[2] { boundaryX, boundaryY });
            if (firstX == -1 && firstY == -1 && (boundaryX != startX || boundaryY != startY))
            {
                firstX = boundaryX;
                firstY = boundaryY;
            }
            for (int i = boundaryDirection + 4; i > boundaryDirection; i--)
            {
                if (i % 4 == 0)
                {
                    if (boundaryY > 0 && labelImg[boundaryX, boundaryY - 1] >= 1)
                    {
                        if (boundaryX == firstX && boundaryY - 1 == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = i + 1;
                        boundaryY = boundaryY - 1;
                        return true;
                    }
                    else
                        continue;
                }
                if (i % 4 == 1)
                {
                    if (boundaryX < OutputWidth - 1 && labelImg[boundaryX + 1, boundaryY] >= 1)
                    {
                        if (boundaryX + 1 == firstX && boundaryY == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = i + 1;
                        boundaryX = boundaryX + 1;
                        return true;
                    }
                    else
                        continue;
                }
                if (i % 4 == 2)
                {
                    if (boundaryY < OutputHeight - 1 && labelImg[boundaryX, boundaryY + 1] >= 1)
                    {
                        if (boundaryX == firstX && boundaryY + 1 == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = i + 1;
                        boundaryY = boundaryY + 1;
                        return true;
                    }
                    else
                        continue;
                }
                if (i % 4 == 3)
                {
                    if (boundaryX > 0 && labelImg[boundaryX - 1, boundaryY] >= 1)
                    {
                        if (boundaryX - 1 == firstX && boundaryY == firstY && boundaryX == startX && boundaryY == startY)
                            return false;
                        boundaryDirection = i + 1;
                        boundaryX = boundaryX - 1;
                        return true;
                    }
                    else
                        continue;
                }
            }
            return false;
        }

        private void labelPixels(int[,] labelImg, int x, int y, int label)
        {
            labelImg[x, y] = label;
            if (x < OutputWidth - 1 && labelImg[x + 1, y] == 0)
                labelImg[x + 1, y] = -1;
            if (y < OutputHeight - 1 && labelImg[x, y + 1] == 0)
                labelImg[x, y + 1] = -1;

        }

        private void getBinaryLabel(int[,] img, int[,] labelImg)
        {
            for (int x = 0; x < InputImage.Size.Width; x++)
            {
                for (int y = 0; y < InputImage.Size.Height; y++)
                {
                    int pixelColor = img[x, y];
                    if (pixelColor < 128)
                        img[x, y] = 0;
                    else
                    {
                        labelImg[x, y] = 1;
                        img[x, y] = 255;
                    }
                }
            }
        }
    }

    public class Circle
    {
        public int r, a, b;
        public Circle(int r, int a, int b)
        {
            this.r = r;
            this.a = a;
            this.b = b;
        }
    }
}
