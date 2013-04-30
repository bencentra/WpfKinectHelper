using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Kinect;

/*
 *  KinectHelper.cs  
 *  
 *  A helper class containing all the important operations for 
 *  interacting with the Microsoft Kinect in a C# WPF Application.
 *  
 *  Version 1.01
 *  Changes:
 *      - Can set the background of the skeletonBitmap using ChangeSkeletonBackgroundColor()
 *                       
 *  Written by Ben Centra, with inspiration from:
 *  - http://www.renauddumont.be/en/2012/kinect-sdk-1-0-1-introduction-a-lapi
 *  - http://msdn.microsoft.com/en-us/library/hh855347.aspx
 */

namespace WpfKinectHelper
{
    /*
     *  KinectHelper - Contains helpful methods and events for working with the Kinect
     */
    class KinectHelper
    {
        // The KincectSensor being used
        private KinectSensor Kinect { get; set; }

        // Coordinate Mapper (for custom conversions beyond the Skeleton)
        public CoordinateMapper PointMapper
        {
            get
            {
                return Kinect.CoordinateMapper;
            }
        }

        // Booleans to track which streams should be enabled
        public bool UseColorImageStream { get; set; } // ColorImageStream (RGB Video)
        public bool UseDepthImageStream { get; set; } // DepthImageStream (Depth Data)
        public bool UseSkeletonStream { get; set; } // SkeletonStream (Skeleton Tracking)
        public bool UseAudioStream { get; set; } // AudioStream 
        
        // ColorImageStream variables
        private byte[] colorStreamData; // Byte array of image data from a ColorImageFrame
        public WriteableBitmap colorBitmap { get; set; } // WriteableBitmap to display the image data

        // DepthImageStream variables
        private DepthImagePixel[] depthStreamData; // DepthImagePixel array of depth data from a DepthImageFrame
        private byte[] depthRgbData; // Byte array to store depth data converted to RGB
        public WriteableBitmap depthBitmap { get; set; } // WriteableBitmap to display the converted depth data

        // SkeletonStream variables
        private Skeleton[] skeletonStreamData; // Skeleton array of Skeletons received in each SkeletonFrame
        private DrawingGroup drawingGroup; // DrawingGroup for rendering the Skeletons
        public DrawingImage skeletonBitmap { get; set; } // Image to render the Skeletons to

        // Skeleton drawing settings
        private const float RenderWidth = 640.0f; // TO-DO: Move to constructor/initialization logic, allow user to set render size
        private const float RenderHeight = 480.0f;
        private const double JointThickness = 3;
        private const double BodyCenterThickness = 10;
        private const double ClipBoundsThickness = 10;
        private Brush backgroundBrush = Brushes.White;
        private readonly Brush centerPointBrush = Brushes.Blue;
        private readonly Brush trackedJointBrush = Brushes.LightBlue;
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        // Other behavioral settings
        private const bool ResetAngleOnStartup = true;

        // Event delegate definitions
        public delegate void ColorDataChangedEvent(object sender, ColorDataChangeEventArgs e); // Color data change event
        public delegate void DepthDataChangedEvent(object sender, DepthDataChangeEventArgs e); // Depth data change event
        public delegate void SkeletonDataChangedEvent(object sender, SkeletonDataChangeEventArgs e); // Skeleton data change event

        // Event delegate instances
        public ColorDataChangedEvent ColorDataChanged; // Color data change
        protected virtual void ColorDataChange(ColorDataChangeEventArgs e)
        {
            if (ColorDataChanged != null)
            {
                ColorDataChanged(this, e);
            }
        }
        public DepthDataChangedEvent DepthDataChanged; // Depth data change
        protected virtual void DepthDataChange(DepthDataChangeEventArgs e)
        {
            if (DepthDataChanged != null)
            {
                DepthDataChanged(this, e);
            }
        }
        public SkeletonDataChangedEvent SkeletonDataChanged; // Skeleton data change
        protected virtual void SkeletonDataChange(SkeletonDataChangeEventArgs e)
        {
            if (SkeletonDataChanged != null)
            {
                SkeletonDataChanged(this, e);
            }
        }
        
        // Default Constructor 
        // NOTE: Everything will have to be enabled manually
        public KinectHelper()
        {
            // By default, use all 3 streams (color, depth, skeleton)
            this.UseColorImageStream = false;
            this.UseDepthImageStream = false;
            this.UseSkeletonStream = false;
        }

        // Constructor with boolean triggers
        // Streams are enabled if corresponding parameter is set to "true"
        public KinectHelper(bool useColor, bool useDepth, bool useSkeleton)
        {
            // Start with all streams disabled
            this.UseColorImageStream = false;
            this.UseDepthImageStream = false;
            this.UseSkeletonStream = false;

            // Enable the various streams if corresponding output Images were provided
            if (useColor)
                this.UseColorImageStream = true;
            if (useDepth)
                this.UseDepthImageStream = true;
            if (useSkeleton)
                this.UseSkeletonStream = true;

            // Initialize the Kinect
            InitializeKinectSensor();
        }

        // Find and enable the first KinectSensor available
        public void InitializeKinectSensor()
        {
            // Look through all sensors and start the first connected one.
            // Requires a Kinect to be plugged in at app startup.
            // TO-DO: Use KinectSensorChooser (Microsoft.Kinect.Toolkit) to allow for plug/unplug
            KinectSensor firstKinect = null;
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    firstKinect = potentialSensor;
                    break;
                }
            }

            // Enable the various streams and start the sensor
            if (this.Kinect == null)
            {
                SetNewKinect(firstKinect);
            }
            // Add a custom StatusChanged event handler to all KinectSensors
            KinectSensor.KinectSensors.StatusChanged += KinectStatusChanged;
        }

        // Set a new KinectSensor as our current Kinect
        private void SetNewKinect(KinectSensor newKinect)
        {
            // If the newKinect is a new sensor, start/stop the appropriate sensors
            if (this.Kinect != newKinect)
            {
                if (this.Kinect != null)
                    StopKinect(this.Kinect);
                if (newKinect != null)
                    StartKinect(newKinect);
            }
            this.Kinect = newKinect;
            if (ResetAngleOnStartup)
                AdjustElevationAngle(0);
        }

        // Shut down an old KinectSensor
        private void StopKinect(KinectSensor oldKinect)
        {
            oldKinect.Stop();
        }

        // Start up a new KinectSensor
        private void StartKinect(KinectSensor newKinect)
        {
            // Enable the ColorStream and listen for ColorFrameReady events
            if (this.UseColorImageStream)
            {
                newKinect.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.colorStreamData = new byte[newKinect.ColorStream.FramePixelDataLength];
                this.colorBitmap = new WriteableBitmap(newKinect.ColorStream.FrameWidth, newKinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);
                newKinect.ColorFrameReady += KinectColorFrameReady;
            }
            // Enable the DepthStream and listen for DepthFrameReady events
            if (this.UseDepthImageStream)
            {
                newKinect.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                this.depthStreamData = new DepthImagePixel[newKinect.DepthStream.FramePixelDataLength];
                this.depthRgbData = new byte[newKinect.DepthStream.FramePixelDataLength * sizeof(int)];
                this.depthBitmap = new WriteableBitmap(newKinect.DepthStream.FrameWidth, newKinect.DepthStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);
                newKinect.DepthFrameReady += KinectDepthFrameReady;
            }
            // Enable the SkeletonStream and listen for SkeletonFrameReady event
            if (this.UseSkeletonStream)
            {
                newKinect.SkeletonStream.Enable();
                this.skeletonStreamData = new Skeleton[newKinect.SkeletonStream.FrameSkeletonArrayLength];
                this.drawingGroup = new DrawingGroup();
                this.skeletonBitmap = new DrawingImage(this.drawingGroup);
                newKinect.SkeletonFrameReady += KinectSkeletonFrameReady;
            }
            // Start the Kinect
            newKinect.Start();
        }

        // Custom Event Handler for a change in KinectSensor status
        private void KinectStatusChanged(object sender, StatusChangedEventArgs e)
        {
            // If a new KinectSensor is connected, set is as our currrent Kinect
            if (e.Sensor != null && e.Status == KinectStatus.Connected)
                SetNewKinect(e.Sensor);
            // If the current Kinect changes it's status, disconnect the current Kinect
            if (e.Sensor == this.Kinect)
                SetNewKinect(null);
        }

        // Event Handler for ColorFrameReady events
        // (A new frame of ColorStream data is available)
        private void KinectColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            // Get the current ColorImageFrame
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    try
                    {
                        // Get the pixel data from the ColorImageFrame
                        colorFrame.CopyPixelDataTo(this.colorStreamData);
                        // Write the pixel data to the colorBitmap image
                        colorBitmap.WritePixels(new Int32Rect(0, 0, colorFrame.Width, colorFrame.Height), colorStreamData, colorFrame.Width * colorFrame.BytesPerPixel, 0);
                        // Dispatch the ColorDataChange event
                        ColorDataChangeEventArgs c = new ColorDataChangeEventArgs(colorStreamData);
                        ColorDataChange(c);
                    }
                    catch (NullReferenceException ex)
                    {
                        Console.WriteLine(ex.TargetSite +" - " + ex.Message);
                    }
                }
            }
        }

        // Event Handler for DepthFrameReady events
        // (A new frame of DepthStream data is available)
        private void KinectDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            // Get the current DepthImageFrame
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    try
                    {
                        // Get the depth data from the DepthImageFrame
                        depthFrame.CopyDepthImagePixelDataTo(depthStreamData);
                        int minDepth = depthFrame.MinDepth;
                        int maxDepth = depthFrame.MaxDepth;
                        // Loop through each pixel and convert to RGB
                        int colorPixelIndex = 0;
                        for (int i = 0; i < depthStreamData.Length; ++i)
                        {
                            short depth = depthStreamData[i].Depth;
                            byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);
                            depthRgbData[colorPixelIndex++] = intensity;
                            depthRgbData[colorPixelIndex++] = intensity;
                            depthRgbData[colorPixelIndex++] = intensity;
                            colorPixelIndex++;
                        }
                        // Write the converted depth data to the depthBitmap image
                        depthBitmap.WritePixels(new Int32Rect(0, 0, depthBitmap.PixelWidth, depthBitmap.PixelHeight), depthRgbData, depthBitmap.PixelWidth * sizeof(int), 0);
                        // Dispatch the DepthDataChange event
                        DepthDataChangeEventArgs d = new DepthDataChangeEventArgs(depthStreamData, depthRgbData);
                        DepthDataChange(d);
                    }
                    catch (NullReferenceException ex)
                    {
                        Console.WriteLine(ex.TargetSite + " - " + ex.Message);
                    }
                }
            }
        }

        // Event Handler for SkeletonFrameReady events
        // (A new frame of SkeletonStream data is available)
        private void KinectSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            // Initialize/empty the skeletonStreamData array
            this.skeletonStreamData = new Skeleton[0];

            // Get the current SkeletonFrame and copy out the data
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    try
                    {
                        // Get the Skeleton data from the SkeletonFrame
                        skeletonStreamData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletonStreamData);
                        // Dispatch the SkeletonDataChange event
                        SkeletonDataChangeEventArgs s = new SkeletonDataChangeEventArgs(skeletonStreamData);
                        SkeletonDataChange(s);
                    }
                    catch (NullReferenceException ex)
                    {
                        Console.WriteLine(ex.TargetSite + " - " + ex.Message);
                    }
                }
            }

            // Create the Skeleton image output
            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a black background the size of our render
                dc.DrawRectangle(backgroundBrush, null, new Rect(0, 0, RenderWidth, RenderHeight));
                
                //Draw each Skeleton
                if (skeletonStreamData.Length != 0)
                {
                    foreach (Skeleton skeleton in skeletonStreamData)
                    {
                        // TO-DO: Render clipped edges

                        if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            DrawSkeletonBonesAndJoints(dc, skeleton.Joints);
                        }
                        else if (skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            DrawSkeletonPosition(dc, skeleton.Position);
                        }
                    }
                }

                // Prevent any drawing outside the render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0, 0, RenderWidth, RenderHeight));
            }
        }

        // Draw all the bones and joints of a Tracked Skeleton
        private void DrawSkeletonBonesAndJoints(DrawingContext dc, JointCollection joints)
        {
            // Render Head and Shoulders
            DrawBone(dc, joints[JointType.Head], joints[JointType.ShoulderCenter]);
            DrawBone(dc, joints[JointType.ShoulderCenter], joints[JointType.ShoulderLeft]);
            DrawBone(dc, joints[JointType.ShoulderCenter], joints[JointType.ShoulderRight]);
            // Render Left Arm
            DrawBone(dc, joints[JointType.ShoulderLeft], joints[JointType.ElbowLeft]);
            DrawBone(dc, joints[JointType.ElbowLeft], joints[JointType.WristLeft]);
            DrawBone(dc, joints[JointType.WristLeft], joints[JointType.HandLeft]);
            // Render Right Arm
            DrawBone(dc, joints[JointType.ShoulderRight], joints[JointType.ElbowRight]);
            DrawBone(dc, joints[JointType.ElbowRight], joints[JointType.WristRight]);
            DrawBone(dc, joints[JointType.WristRight], joints[JointType.HandRight]);
            // Render Body and Hips
            DrawBone(dc, joints[JointType.ShoulderCenter], joints[JointType.Spine]);
            DrawBone(dc, joints[JointType.Spine], joints[JointType.HipCenter]);
            DrawBone(dc, joints[JointType.HipCenter], joints[JointType.HipLeft]);
            DrawBone(dc, joints[JointType.HipCenter], joints[JointType.HipRight]);
            // Render Left Leg
            DrawBone(dc, joints[JointType.HipLeft], joints[JointType.KneeLeft]);
            DrawBone(dc, joints[JointType.KneeLeft], joints[JointType.AnkleLeft]);
            DrawBone(dc, joints[JointType.AnkleLeft], joints[JointType.FootLeft]);
            // Render Right Leg
            DrawBone(dc, joints[JointType.HipRight], joints[JointType.KneeRight]);
            DrawBone(dc, joints[JointType.KneeRight], joints[JointType.AnkleRight]);
            DrawBone(dc, joints[JointType.AnkleRight], joints[JointType.FootRight]);
            // Render Joints
            foreach (Joint joint in joints)
            {
                if (joint.TrackingState == JointTrackingState.Tracked)
                    dc.DrawEllipse(this.trackedJointBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                else if (joint.TrackingState == JointTrackingState.Inferred)
                    dc.DrawEllipse(this.inferredJointBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
            }
        }

        // Draw just the position of a PositionOnly Skeleton
        private void DrawSkeletonPosition(DrawingContext dc, SkeletonPoint position)
        {
            dc.DrawEllipse(this.centerPointBrush, null, SkeletonPointToScreen(position), BodyCenterThickness, BodyCenterThickness);
        }

        // Draw a Skeleton bone
        private void DrawBone(DrawingContext dc, Joint from, Joint to)
        {
            // If the bone is not being tracked (either joint not tracked), ignore it
            if (from.TrackingState == JointTrackingState.NotTracked || to.TrackingState == JointTrackingState.NotTracked)
                return;
            // If the bone is inferred (either joint inferred), draw a thin line
            if (from.TrackingState == JointTrackingState.Inferred || to.TrackingState == JointTrackingState.Inferred)
                dc.DrawLine(inferredBonePen, SkeletonPointToScreen(from.Position), SkeletonPointToScreen(to.Position));
            // If the bone is tracked (both joints tracked), draw a bold line
            if (from.TrackingState == JointTrackingState.Tracked && to.TrackingState == JointTrackingState.Tracked)
                dc.DrawLine(trackedBonePen, SkeletonPointToScreen(from.Position), SkeletonPointToScreen(to.Position));
        }

        // Map a SkeletonPoint to a Point that can be used for drawing
        public Point SkeletonPointToScreen(SkeletonPoint point)
        {
            // TO-DO: Handle NullReferenceException somehow
            DepthImagePoint depthPoint = Kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(point, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        // Call this to Stop the current Kinect before closing the program
        public void ShutdownHelper()
        {
            SetNewKinect(null);
        }

        // Toggle the SkeletonStream tracking mode (Seated vs. Default)
        // (Should be used in conjunction with a Checkbox "Changed" event handler)
        public void ToggleSeatedMode(bool useSeatedMode)
        {
            if (this.Kinect != null)
            {
                if (useSeatedMode)
                    this.Kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                else
                    this.Kinect.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
            }
        }

        // Adjust the motor tilt angle of the Kinect
        // (Should be used in conjunction with a Slider "Changed" or Button "Click" event handler
        public void AdjustElevationAngle(int newAngle)
        {
            if (this.Kinect != null)
            {
                if (newAngle > this.Kinect.MaxElevationAngle)
                    newAngle = this.Kinect.MaxElevationAngle;
                else if (newAngle < this.Kinect.MinElevationAngle)
                    newAngle = this.Kinect.MinElevationAngle;
                try
                {
                    Kinect.ElevationAngle = newAngle;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.TargetSite + " - " + ex.Message);
                }
            }
        }

        // Change the background colors of the skeletonBitmap
        public void ChangeSkeletonBackgroundColor(Brush brush)
        {
            backgroundBrush = brush;
        }
    }

    // TO-DO: Move these into separate class files?

    /*
     *  ColorDataChangeEventArgs - Information for custom event fired when ColorStream data changes
     */
    public class ColorDataChangeEventArgs
    {
        public readonly byte[] colorData;

        public ColorDataChangeEventArgs(byte[] colorData)
        {
            this.colorData = colorData;
        }
    }

    /*
     *  DepthDataChangeEventArgs - Information for custom event fired when DepthStream data changes
     */
    public class DepthDataChangeEventArgs
    {
        public readonly DepthImagePixel[] depthData;
        public readonly byte[] rgbData;

        public DepthDataChangeEventArgs(DepthImagePixel[] depthData, byte[] rgbData)
        {
            this.depthData = depthData;
            this.rgbData = rgbData;
        }
    }

    /*
     *  SkeletonDataChangeEventArgs - Information for custom event fired when SkeletonStream data changes
     */ 
    public class SkeletonDataChangeEventArgs
    {
        public readonly Skeleton[] skeletons;

        public SkeletonDataChangeEventArgs(Skeleton[] skeletons)
        {
            this.skeletons = skeletons;
        }
    }

    /*
     * AudioDataChangeEventArgs - Information for custom event fired when a new section of the audio buffer is ready
     */
    /*
    public class AudioDataChangeEventArgs
    {
        public readonly byte[] audioBuffer;

        public AudioDataChangeEventArgs(byte[] audioBuffer)
        {
            this.audioBuffer = audioBuffer;
        }
    }
    */
 
}