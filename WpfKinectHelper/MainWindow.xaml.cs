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

namespace WpfKinectHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Create a new KinectHelper
        private KinectHelper helper;

        // Construct a new Window
        public MainWindow()
        {
            InitializeComponent();
        }

        // Event Handler for the Window's Loaded event
        // (Use to initialize the KinectHelper)
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Enable the Kinect and data streams manually with default constructor
            helper = new KinectHelper();
            helper.UseColorImageStream = true;
            helper.UseDepthImageStream = true;
            helper.UseSkeletonStream = true;
            helper.InitializeKinectSensor();
            
            // Enable the Kinect and all data streams with shorthand constructor
            //helper = new KinectHelper(true, true, true);

            // Link the UI Images with the images produced by the Kinect
            _colorImage.Source = helper.colorBitmap;
            _depthImage.Source = helper.depthBitmap;
            _skeletonImage.Source = helper.skeletonBitmap;

            // Listen for data stream change events 
            helper.ColorDataChanged += new KinectHelper.ColorDataChangedEvent(ColorDataChange);
            helper.DepthDataChanged += new KinectHelper.DepthDataChangedEvent(DepthDataChange);
            helper.SkeletonDataChanged += new KinectHelper.SkeletonDataChangedEvent(SkeletonDataChange);
        }

        // Event Handler for the Window's Closing event
        // (Use to safetly stop the Kinect)
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            helper.ShutdownHelper();
        }

        // Event Handler for clicking the tiltButton
        // (Use to adjust the Kinect's motor tilt)
        private void tiltButton_Click(object sender, RoutedEventArgs e)
        {
            helper.AdjustElevationAngle((int)_tiltSlider.Value);
        }

        // Event Handler for checking the modeCheckbox
        // (Use to enable "Seated Mode" for Skeleton Tracking)
        private void modeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            helper.ToggleSeatedMode(true);
        }

        // Event Handler for unchecking the modeCheckbox
        // (Use to disable "Seated Mode" for Skeleton Tracking)
        private void modeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            helper.ToggleSeatedMode(false);
        }

        // Event Handler for changes in Color data
        // Allows for direct access to the color data
        private void ColorDataChange(object sender, ColorDataChangeEventArgs e)
        {
            Console.WriteLine("Here's the first color byte: " + e.colorData[0].ToString());
        }

        // Event Handler for changes in Depth data
        // Allows for direct access to the depth (and converted RGB) data
        private void DepthDataChange(object sender, DepthDataChangeEventArgs e)
        {
            // TO-DO: Is the rgbData returning anything useful?
            Console.WriteLine("Here's the first depth (rgb) byte: " + e.rgbData[e.rgbData.Length/4].ToString());
        }

        // Event Handler for changes in Skeleton data
        // Allows for direct access to the Skeleton data
        private void SkeletonDataChange(object sender, SkeletonDataChangeEventArgs e)
        {
            Skeleton skel = e.skeletons[0];
            Point skelPoint = helper.SkeletonPointToScreen(skel.Position);
            Console.WriteLine("Skeleton 1 at (" + skelPoint.X.ToString() + "," + skelPoint.Y.ToString() + ")!");
        }
    }
}
