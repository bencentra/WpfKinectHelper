WpfKinectHelper
===============
A helper class designed to make working with the Microsoft Kinect in C# WPF Applications a breeze.  

(The Kinect SDK and a Microsoft Kinect are required)

Made by Ben Centra (bencentra@csh.rit.edu)

Changes
-------
Version 1.02 - 06/03/13
* Added InfraredStream option, overrides output of ColorImageStream (Kinect can only do one or the other)
* Added AudioStream, can get audio buffer, beam angle, and source angle

Version 1.01 - 2/28/13     
* Can set the background of the skeletonBitmap using ChangeSkeletonBackgroundColor()    
* Added PointMapper to allow for point-to-screen conversion outside of drawing the skeleton

What does it do?
----------------
*  Instantly sets up Color, Depth, Skeleton, and Audio data streams  
*  Converts Color and Depth data for easy display  
*  Draws Skeleton bones and joints in both Default and Seated modes  
*  Access to Kinect motor for angle adjustment  
*  Access to live stream data through custom events

How do I use it?
----------------
Start by creating a new C# WPF Application in Visual Studio and adding 'KinectHelper.cs' to the project.

Next, add the appropriate Controls to MainWindow.xaml, such as:

* Images for the Color, Depth, and Skeleton data
* Slider for adjusting the motor angle  
* Checkbox for toggling Seated mode  

Then, add a KinectHelper object to MainWindow.xaml.cs:

	// Create a new KinectHelper
    private KinectHelper helper;

Instantiate the KinectHelper inside of a "Loaded" event handler for the Main Window:

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Enable the Kinect and data streams manually with default constructor
        helper = new KinectHelper();
        helper.UseColorImageStream = true;
        helper.UseDepthImageStream = true;
        helper.UseSkeletonStream = true;
        helper.InitializeKinectSensor();
	}
    
You can also use the shorthand constructor for the KinectHelper to do all the above in one line:

	// Enable the Kinect and all data streams with the shorthand constructor
	// KinectHelper(bool useColor, bool useDepth, bool useSkeleton, bool useAudio, bool useInfrared)
	helper = new KinectHelper(true, true, true, false, false); 

To view the output of the Kinect's data streams, set the Source property of the Image controls created in MainWindow.xaml:

	// Link the UI Images with the images produced by the Kinect
    _colorImage.Source = helper.colorBitmap;
    _depthImage.Source = helper.depthBitmap;
    _skeletonImage.Source = helper.skeletonBitmap

You can use some additional KinectHelper methods to control the motor angle and toggle Seated mode in event handlers for the other controls created in MainWindow.xaml:

	// Adjust the motor angle using the value of a Slider control (value range -27 to +27)
	helper.AdjustElevationAngle((int)_tiltSlider.Value); 
	// Toggle Skeleton Tracking mode (true = Seated, false = Default)
	helper.ToggleSeatedMode(true); 
	
You can also access stream data directly using custom events. For example, to listen for SkeletonDataChange events, first add the following to your Main Window Loaded event:

	// Listen for data stream change events 
    helper.SkeletonDataChanged += new KinectHelper.SkeletonDataChangedEvent(SkeletonDataChange);

Then, add the event handler method ("SkeletonDataChange") to MainWindow.xaml.cs:

	// Event Handler for changes in Skeleton data
    // Allows for direct access to the Skeleton data
    private void SkeletonDataChange(object sender, SkeletonDataChangeEventArgs e)
    {
        //Get the position of the first Skeleton
        Skeleton skel = e.skeletons[0];
        Point skelPoint = helper.SkeletonPointToScreen(skel.Position);
        Console.WriteLine("Skeleton 1 at (" + skelPoint.X.ToString() + "," + skelPoint.Y.ToString() + ")!");
    }
	
To-Do's
-------
* Add more features (Face Tracking, Gestures, other SDK 1.7 stuff, etc)
* Allow user to set Audio Stream options before initializing

Sources
-------
* Kinect for Windows SDK Documentation: http://msdn.microsoft.com/en-us/library/hh855347.aspx
* Kinect SDK Tutorials by Renaud Dumont: http://www.renauddumont.be/en/2012/kinect-sdk-1-0-1-introduction-a-lapi