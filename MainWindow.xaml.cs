//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, int dwFlags, IntPtr dwExtraInfo);

        const byte KEYEVENTF_EXTENDEDKEY = 0x01;
        const byte KEYEVENTF_KEYUP = 0x02;
        const byte Left_key = 0x25;
        const byte Up_key = 0x26;
        const byte Right_key = 0x27;
        const byte Down_key = 0x28;

        const byte W_key =0x57;
        const byte A_key =0x41;
        const byte S_key =0x53;
        const byte D_key =0x44;

        const byte N_key = 0x4E;
        const byte H_key = 0x48;
        const byte J_key = 0x4A;
        const byte M_key = 0x4D;



        int reset = 0;
        float centerY = 0.5F;
        int punch=0;
        int mode = 0;
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

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

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            if(skeleton.Joints[JointType.WristLeft].Position.Y > skeleton.Joints[JointType.Head].Position.Y && skeleton.Joints[JointType.WristRight].Position.Y > skeleton.Joints[JointType.Head].Position.Y)
            {
                if (mode == 0)
                    mode = 1;
                else
                    mode = 0;
                System.Threading.Thread.Sleep(1000);
                centerY = skeletㄙon.Joints[JointType.ShoulderCenter].Position.Y;
            }
            

            float move = skeleton.Joints[JointType.ShoulderCenter].Position.X - skeleton.Joints[JointType.HipCenter].Position.X;
            textBox.Text = "y:" + (skeleton.Joints[JointType.WristRight].Position.X - skeleton.Joints[JointType.WristLeft].Position.X) + "\n";

            
            //Right mode
            if (mode == 0)
            {
                //A,D button
                if (move > 0.12)
                {
                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                }

                else if (move < -0.13)
                {
                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                }

                else
                {
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                }

                //jump and crouch
                if (skeleton.Joints[JointType.ShoulderCenter].Position.Y - centerY > 0.2)
                {
                    keybd_event(W_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(100);
                    keybd_event(W_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                }
                if (skeleton.Joints[JointType.ShoulderCenter].Position.Y - centerY < -0.2)
                {
                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                }
                else
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                //crit
                if (skeleton.Joints[JointType.Head].Position.Y < skeleton.Joints[JointType.ShoulderCenter].Position.Y)
                {
                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    System.Threading.Thread.Sleep(200);
                }

                //punch
                if (skeleton.Joints[JointType.WristRight].Position.Y - skeleton.Joints[JointType.ShoulderCenter].Position.Y > 0.1)
                {
                    //sholiukan
                    punch = 1;
                    System.Threading.Thread.Sleep(50);
                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    System.Threading.Thread.Sleep(500);
                }
                if (skeleton.Joints[JointType.WristRight].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X > 0.5 && punch == 0)
                {
                    System.Threading.Thread.Sleep(50);
                    //hadokan
                    if (skeleton.Joints[JointType.WristLeft].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X > 0.3)
                    {
                        keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                        keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                        keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                        keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        System.Threading.Thread.Sleep(200);
                    }
                    //6p
                    if (skeleton.Joints[JointType.ElbowLeft].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X < -0.3)
                    {
                        keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(100);
                        keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        System.Threading.Thread.Sleep(200);
                    }
                    //p
                    else
                    {
                        keybd_event(H_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(H_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    }

                }
                punch = 0;

                //kick
                if (skeleton.Joints[JointType.AnkleRight].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X > 0.6)
                {                  
                    //k
                        keybd_event(N_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(N_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        System.Threading.Thread.Sleep(100);                    
                }
                if (skeleton.Joints[JointType.KneeRight].Position.Y - skeleton.Joints[JointType.HipCenter].Position.Y > 0.1)
                {
                    //4k
                    System.Threading.Thread.Sleep(50);
                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(M_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(100);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(M_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    System.Threading.Thread.Sleep(200);
                }

                
                
            }


            //Left mode
            if (mode == 1)
            {
                //A,D button
                if (move > 0.12)
                {
                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                }

                else if (move < -0.13)
                {
                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                }

                else
                {
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                }

                //jump and crouch
                if (skeleton.Joints[JointType.ShoulderCenter].Position.Y - centerY > 0.2)
                {
                    keybd_event(W_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(100);
                    keybd_event(W_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                }
                if (skeleton.Joints[JointType.ShoulderCenter].Position.Y - centerY < -0.2)
                {
                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                }
                else
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                //crit
                if (skeleton.Joints[JointType.Head].Position.Y< skeleton.Joints[JointType.ShoulderCenter].Position.Y)
                {
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    System.Threading.Thread.Sleep(200);
                }

                //punch
                if (skeleton.Joints[JointType.WristLeft].Position.Y - skeleton.Joints[JointType.ShoulderCenter].Position.Y > 0.1)
                {
                    //sholiukan
                    punch = 1;
                    System.Threading.Thread.Sleep(50);
                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                    keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(50);
                    keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    System.Threading.Thread.Sleep(500);
                }
                if (skeleton.Joints[JointType.WristLeft].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X < -0.5 && punch == 0)
                {
                    System.Threading.Thread.Sleep(50);
                    //hadokan
                    if (skeleton.Joints[JointType.WristRight].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X < -0.3)
                    {
                        keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                        keybd_event(S_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(S_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                        keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);

                        keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        System.Threading.Thread.Sleep(200);
                    }
                    //6p
                    if (skeleton.Joints[JointType.ElbowRight].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X > 0.3)
                    {
                        keybd_event(A_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        keybd_event(J_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(100);
                        keybd_event(A_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        keybd_event(J_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        System.Threading.Thread.Sleep(200);
                    }
                    //p
                    else
                    {
                        keybd_event(H_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(H_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    }

                }
                punch = 0;

                //kick
                if (skeleton.Joints[JointType.AnkleLeft].Position.X - skeleton.Joints[JointType.ShoulderCenter].Position.X < -0.6)
                {
                    //k
                        keybd_event(N_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                        System.Threading.Thread.Sleep(50);
                        keybd_event(N_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                        System.Threading.Thread.Sleep(100);
                }
                if (skeleton.Joints[JointType.KneeLeft].Position.Y - skeleton.Joints[JointType.HipCenter].Position.Y > 0.1)
                {
                    //4k
                    System.Threading.Thread.Sleep(50);
                    keybd_event(D_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    keybd_event(M_key, 0, KEYEVENTF_EXTENDEDKEY, (IntPtr)0);
                    System.Threading.Thread.Sleep(100);
                    keybd_event(D_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    keybd_event(M_key, 0, KEYEVENTF_KEYUP, (IntPtr)0);
                    System.Threading.Thread.Sleep(200);
                }


                


            }


            
    



            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);
 
            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;                    
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;                    
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }
    }
}