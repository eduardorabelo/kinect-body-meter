using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.IO;

namespace VR_KinectBodyMeter
{
    /// <summary>
    /// Interaction logic for BodyMeter.xaml
    /// </summary>
    public partial class BodyMeter : Window
    {
        KinectSensor sensor;

        #region attributes needed for body image
        /// <summary>
        /// Format we will use for the depth stream
        /// </summary>
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        /// <summary>
        /// Format we will use for the color stream
        /// </summary>
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Bitmap that will hold opacity mask information
        /// </summary>
        private WriteableBitmap playerOpacityMaskImage = null;

        /// <summary>
        /// Intermediate storage for the depth data received from the sensor
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;

        /// <summary>
        /// Intermediate storage for the green screen opacity mask
        /// </summary>
        private int[] greenScreenPixelData;

        /// <summary>
        /// Intermediate storage for the depth to color mapping
        /// </summary>
        private ColorImagePoint[] colorCoordinates;

        /// <summary>
        /// Inverse scaling factor between color and depth
        /// </summary>
        private int colorToDepthDivisor;

        /// <summary>
        /// Width of the depth image
        /// </summary>
        private int depthWidth;

        /// <summary>
        /// Height of the depth image
        /// </summary>
        private int depthHeight;

        /// <summary>
        /// Indicates opaque in an opacity mask
        /// </summary>
        private int opaquePixelValue = -1;

        private int[] right;
        private int[] left;
        private double heightInPixels;
        #endregion

        public BodyMeter()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            sensor = KinectSensor.KinectSensors.Where(x => x.Status == KinectStatus.Connected).FirstOrDefault();

            if (sensor != null)
            {
                sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(Sensor_SkeletonFrameReady);
                sensor.SkeletonStream.Enable();

                // Turn on the depth stream to receive depth frames
                this.sensor.DepthStream.Enable(DepthFormat);

                this.depthWidth = this.sensor.DepthStream.FrameWidth;

                this.depthHeight = this.sensor.DepthStream.FrameHeight;

                this.sensor.ColorStream.Enable(ColorFormat);

                int colorWidth = this.sensor.ColorStream.FrameWidth;
                int colorHeight = this.sensor.ColorStream.FrameHeight;

                this.colorToDepthDivisor = colorWidth / this.depthWidth;

                // Turn on to get player masks
                this.sensor.SkeletonStream.Enable();

                // Allocate space to put the depth pixels we'll receive
                this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];

                // Allocate space to put the color pixels we'll create
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                this.greenScreenPixelData = new int[this.sensor.DepthStream.FramePixelDataLength];

                this.colorCoordinates = new ColorImagePoint[this.sensor.DepthStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(colorWidth, colorHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.MaskedColor.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new depth frame data
                this.sensor.AllFramesReady += this.SensorAllFramesReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
        }

        void Sensor_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            using (var frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    canvas.Children.Clear();

                    Skeleton[] skeletons = new Skeleton[frame.SkeletonArrayLength];

                    frame.CopySkeletonDataTo(skeletons);

                    var skeleton = skeletons.Where(s => s.TrackingState == SkeletonTrackingState.Tracked).FirstOrDefault();

                    if (skeleton != null)
                    {
                        DrawSkelet(skeleton);
                    }
                }
            }
        }

        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, so nothing to do
            if (null == this.sensor)
            {
                return;
            }

            bool depthReceived = false;
            bool colorReceived = false;

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    depthReceived = true;
                }
            }

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (null != colorFrame)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    colorReceived = true;
                }
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == depthReceived)
            {
                this.sensor.CoordinateMapper.MapDepthFrameToColorFrame(
                    DepthFormat,
                    this.depthPixels,
                    ColorFormat,
                    this.colorCoordinates);

                Array.Clear(this.greenScreenPixelData, 0, this.greenScreenPixelData.Length);

                left = new int[this.depthHeight]; // left side of the picture (right side of human)
                right = new int[this.depthHeight]; // right side of the picture (left side of human)

                //init result arrays
                for (int y = 0; y < this.depthHeight; ++y)
                {
                    left[y] = -1;
                    right[y] = -1;
                }

                int maxY = 0;
                int minY = this.depthHeight;

                // loop over each row and column of the depth
                for (int y = 0; y < this.depthHeight; ++y)
                {
                    for (int x = 0; x < this.depthWidth; ++x)
                    {
                        // calculate index into depth array
                        int depthIndex = x + (y * this.depthWidth);

                        DepthImagePixel depthPixel = this.depthPixels[depthIndex];

                        int player = depthPixel.PlayerIndex;

                        // if we're tracking a player for the current pixel, do green screen
                        if (player > 0)
                        {
                            // retrieve the depth to color mapping for the current depth pixel
                            ColorImagePoint colorImagePoint = this.colorCoordinates[depthIndex];

                            // scale color coordinates to depth resolution
                            int colorInDepthX = colorImagePoint.X / this.colorToDepthDivisor;
                            int colorInDepthY = colorImagePoint.Y / this.colorToDepthDivisor;

                            // make sure the depth pixel maps to a valid point in color space
                            // check y > 0 and y < depthHeight to make sure we don't write outside of the array
                            // check x > 0 instead of >= 0 since to fill gaps we set opaque current pixel plus the one to the left
                            // because of how the sensor works it is more correct to do it this way than to set to the right
                            if (colorInDepthX > 0 && colorInDepthX < this.depthWidth && colorInDepthY >= 0 && colorInDepthY < this.depthHeight)
                            {
                                // calculate index into the green screen pixel array
                                int greenScreenIndex = colorInDepthX + (colorInDepthY * this.depthWidth);

                                /*
                                 * Detect left and right edges. Should be updated to detect space between legs and stuff like that.
                                 */
                                if (left[y] == -1)
                                {
                                    left[y] = x;
                                }
                                right[y] = x;

                                /*
                                 * Detect maximum and minimum value of Y coordinate for the body. Values are used to calculate pixel - meter ratio.
                                 */ 
                                if (y > maxY)
                                {
                                    maxY = y;
                                }
                                if (y < minY)
                                {
                                    minY = y;
                                }

                                // set opaque
                                this.greenScreenPixelData[greenScreenIndex] = opaquePixelValue;

                                // compensate for depth/color not corresponding exactly by setting the pixel 
                                // to the left to opaque as well
                                this.greenScreenPixelData[greenScreenIndex - 1] = opaquePixelValue;
                            }
                        }
                    }
                }
                heightInPixels = maxY - minY;
            }

            // do our processing outside of the using block
            // so that we return resources to the kinect as soon as possible
            if (true == colorReceived)
            {
                // Write the pixel data into our bitmap
                this.colorBitmap.WritePixels(
                    new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                    this.colorPixels,
                    this.colorBitmap.PixelWidth * sizeof(int),
                    0);

                if (this.playerOpacityMaskImage == null)
                {
                    this.playerOpacityMaskImage = new WriteableBitmap(
                        this.depthWidth,
                        this.depthHeight,
                        96,
                        96,
                        PixelFormats.Bgra32,
                        null);

                    MaskedColor.OpacityMask = new ImageBrush { ImageSource = this.playerOpacityMaskImage };
                }

                this.playerOpacityMaskImage.WritePixels(
                    new Int32Rect(0, 0, this.depthWidth, this.depthHeight),
                    this.greenScreenPixelData,
                    this.depthWidth * ((this.playerOpacityMaskImage.Format.BitsPerPixel + 7) / 8),
                    0);
            }
        }

        private void DrawSkelet(Skeleton skeleton)
        {
            // Draw skeleton joints.
            foreach (JointType joint in Enum.GetValues(typeof(JointType)))
            {
                DrawJoint(skeleton.Joints[joint].ScaleTo(640, 480));
            }

            // Calculate measurments
            double height = Math.Round(skeleton.Height(), 2);
            double armLength = Math.Round(skeleton.ArmLength(), 2);
            double legLength = Math.Round(skeleton.LegLength(), 2);
            double shoulderBreadth = Math.Round(skeleton.ShoulderBreadth(), 2);

            // Display measurments.
            tblHeight.Text = "Height: " + height.ToString() + "m";
            tblArmLength.Text = "Arm: " + armLength.ToString() + "m";
            tblLegLength.Text = "Leg: " + legLength.ToString() + "m";
            tblShoulderBreadth.Text = "Chest: " + shoulderBreadth.ToString() + "m";

            /*
             * Detect width of chest.
             */
            Joint chestJoint = skeleton.Joints[JointType.ShoulderCenter].ScaleTo(640, 480);
            int chestOffset = 10; // move down from chest joint
            int chestHeight = (int) chestJoint.Position.Y;//TODO check if this works (paint chest line on screen)
            int width = right[chestHeight + chestOffset] - left[chestHeight + chestOffset];
            double pixelMeterRatio = height / heightInPixels;


            tblWidth.Text = "Width: " + Math.Round((width * pixelMeterRatio), 2) + "m";

            // if (newSensor != null){
            //    DepthImagePoint depthPoint = this.newSensor.MapSkeletonPointToDepth(
            //                                                                skelpoint,
            //                                                                 DepthImageFormat.Resolution640x480Fps30);
            //    return new System.Drawing.Point(depthPoint.X, depthPoint.Y);
            //}else
            //return new System.Drawing.Point();
        //}
            
        }

        private void DrawJoint(Joint joint)
        {
            Ellipse ellipse = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush(Colors.Black)
            };

            Canvas.SetLeft(ellipse, joint.Position.X);
            Canvas.SetTop(ellipse, joint.Position.Y);

            canvas.Children.Add(ellipse);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor = null;
            }
        }
    }
}
