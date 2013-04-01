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

namespace VR_KinectBodyMeter
{
    /// <summary>
    /// Interaction logic for BodyMeter.xaml
    /// </summary>
    public partial class BodyMeter : Window
    {
        KinectSensor _sensor;

        public BodyMeter()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _sensor = KinectSensor.KinectSensors.Where(x => x.Status == KinectStatus.Connected).FirstOrDefault();

            if (_sensor != null)
            {
                _sensor.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(Sensor_SkeletonFrameReady);
                _sensor.SkeletonStream.Enable();

                _sensor.Start();
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
                    }
                }
            }
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
    }
}
