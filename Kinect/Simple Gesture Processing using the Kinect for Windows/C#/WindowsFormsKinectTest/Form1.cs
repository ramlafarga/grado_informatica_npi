﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using GestureFramework;
using WindowsInput;

namespace WindowsFormsKinectTest
{
    public partial class Form1 : Form
    {
        private KinectSensorChooser _chooser;
        private Bitmap _bitmap;

        private GestureMap _gestureMap;
        private Dictionary<int, GestureMapState> _gestureMaps;
        private const string GestureFileName = "gestures.xml";

        public int PlayerId;
        private bool _readyGestureDetected = false;
        private Pen _arml = new Pen(Color.Red, 5);
        private Pen _armr = new Pen(Color.Red, 5);

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // First Load the XML File that contains the application configuration
            _gestureMap = new GestureMap();
            _gestureMap.LoadGesturesFromXml(GestureFileName);

            _chooser = new KinectSensorChooser();
            _chooser.KinectChanged += ChooserSensorChanged;
            _chooser.Start();

            // Instantiate the in memory representation of the gesture state for each player
            _gestureMaps = new Dictionary<int, GestureMapState>();
        }

        void ChooserSensorChanged(object sender, KinectChangedEventArgs e)
        {
            var old = e.OldSensor;
            StopKinect(old);

            var newsensor = e.NewSensor;
            if (newsensor == null)
            {
                return;
            }

            newsensor.SkeletonStream.Enable();

            newsensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            newsensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            newsensor.AllFramesReady += SensorAllFramesReady;

            try
            {
                newsensor.Start();
                rtbMessages.Text = "Kinect Started" + "\r";
                mInstructions.Text = "1. To start, pass your right knee to the right of your right shoulder, the skeleton should turn on green.\r";
            }
            catch (System.IO.IOException)
            {
                rtbMessages.Text = "Kinect Not Started" + "\r";
                //maybe another app is using Kinect
                _chooser.TryResolveConflict();
            }
        }

        private void StopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                if (sensor.IsRunning)
                {
                    sensor.Stop();
                    sensor.AudioSource.Stop();
                }
            }
        }

        void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (_gestureMap.MessagesWaiting)
            {
                foreach (var msg in _gestureMap.Messages)
                {
                    rtbMessages.AppendText(msg + "\r");
                }
                rtbMessages.ScrollToCaret();
                _gestureMap.MessagesWaiting = false;
            }

            SensorDepthFrameReady(e);
            SensorSkeletonFrameReady(e);
            video.Image = _bitmap;
        }


        double distance(Joint j1, Joint j2)
        {
            return Math.Sqrt(Math.Pow(Math.Abs(j1.Position.X - j2.Position.X), 2) + Math.Pow(Math.Abs(j1.Position.Y - j2.Position.Y), 2) + Math.Pow(Math.Abs(j1.Position.Z - j2.Position.Z), 2));
        }

        void SensorSkeletonFrameReady(AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if (skeletonFrameData == null)
                {
                    return;
                }

                var allSkeletons = new Skeleton[skeletonFrameData.SkeletonArrayLength];
                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                foreach (Skeleton sd in allSkeletons)
                {
                    // If this skeleton is no longer being tracked, skip it
                    if (sd.TrackingState != SkeletonTrackingState.Tracked)
                    {
                        continue;
                    }

                    // If there is not already a gesture state map for this skeleton, then create one
                    if (!_gestureMaps.ContainsKey(sd.TrackingId))
                    {
                        var mapstate = new GestureMapState(_gestureMap);
                        _gestureMaps.Add(sd.TrackingId, mapstate);
                    }

                    var keycode = _gestureMaps[sd.TrackingId].Evaluate(sd, false, _bitmap.Width, _bitmap.Height);
                    GetWaitingMessages(_gestureMaps);

                    if (keycode == VirtualKeyCode.ACCEPT)
                    {
                        var kneer = sd.Joints[JointType.KneeRight];
                        var shoulderr = sd.Joints[JointType.ShoulderRight];

                        var d = distance(kneer, shoulderr);
                        mInstructions.Text = "d: " + d ;

                        //if (d > (d + (d * .1)))
                        //if (d > 0.66)
                        //{

                            rtbMessages.AppendText("READY TO GOOOOOOOO " + sd.TrackingId + "\r");
                            rtbMessages.ScrollToCaret();
                            _gestureMaps[sd.TrackingId].ResetAll(sd);
                            _readyGestureDetected = true;
                            mInstructions.Text = "2. In order to display the next slide, move your right wrist above your right hip and put it down." +
                                "To display the previous slide, do the same with your left wrist\r" +
                                "3. To disable futher processing, pass your left Knee to the left of your left shoulder\r";
                            _armr = new Pen(Color.Green, 5);
                            _arml = new Pen(Color.Green, 5);
                        //}

                        
                    }
                    else if (keycode == VirtualKeyCode.CANCEL)
                    {
                        rtbMessages.AppendText("DISABLING!! " + sd.TrackingId + "\r");
                        rtbMessages.ScrollToCaret();
                        _gestureMaps[sd.TrackingId].ResetAll(sd);
                        _readyGestureDetected = false;

                        _armr = new Pen(Color.Red, 5);
                        _arml = new Pen(Color.Red, 5);
                    }

                    else if (keycode != VirtualKeyCode.NONAME && _readyGestureDetected)
                    {
                        rtbMessages.AppendText("Gesture accepted\r");
                        rtbMessages.ScrollToCaret();
                        rtbMessages.AppendText("Command passed to System: " + keycode + "\r");
                        rtbMessages.ScrollToCaret();
                        InputSimulator.SimulateKeyPress(keycode);
                        _gestureMaps[sd.TrackingId].ResetAll(sd);

                        if (keycode == VirtualKeyCode.NEXT)
                        {
                            _armr = new Pen(Color.DeepSkyBlue, 10);
                            _arml = new Pen(Color.Green, 5);
                        } else if (keycode == VirtualKeyCode.PRIOR)
                        {
                            _arml = new Pen(Color.DeepPink, 10);
                            _armr = new Pen(Color.Green, 5);
                        }
                    }

                    // This break prevents multiple player data from being confused during evaluation.
                    // If one were going to dis-allow multiple players, this trackingId would facilitate
                    // that feat.
                    PlayerId = sd.TrackingId;


                    if (_bitmap != null)
                        _bitmap = AddSkeletonToDepthBitmap(sd, _bitmap, true);

                }
            }
        }

        /// <summary>
        /// This method draws the joint dots and skeleton on the depth image of the depth display
        /// </summary>
        /// <param name="skeleton"></param>
        /// <param name="bitmap"></param>
        /// <param name="isActive"> </param>
        /// <returns></returns>
        private Bitmap AddSkeletonToDepthBitmap(Skeleton skeleton, Bitmap bitmap, bool isActive)
        {
            Pen pen = new Pen(Color.Red, 5);

            var gobject = Graphics.FromImage(bitmap);

            if (_readyGestureDetected)
            {
                pen = new Pen(Color.Green, 3);
            }
            else if (isActive)
            {
                pen = new Pen(Color.Red, 5);
            }

            var head = CalculateJointPosition(bitmap, skeleton.Joints[JointType.Head]);
            var neck = CalculateJointPosition(bitmap, skeleton.Joints[JointType.ShoulderCenter]);
            var rightshoulder = CalculateJointPosition(bitmap, skeleton.Joints[JointType.ShoulderRight]);
            var leftshoulder = CalculateJointPosition(bitmap, skeleton.Joints[JointType.ShoulderLeft]);
            var rightelbow = CalculateJointPosition(bitmap, skeleton.Joints[JointType.ElbowRight]);
            var leftelbow = CalculateJointPosition(bitmap, skeleton.Joints[JointType.ElbowLeft]);
            var rightwrist = CalculateJointPosition(bitmap, skeleton.Joints[JointType.WristRight]);
            var leftwrist = CalculateJointPosition(bitmap, skeleton.Joints[JointType.WristLeft]);

            //var spine = CalculateJointPosition(bitmap, skeleton.Joints[JointType.Spine]);
            var hipcenter = CalculateJointPosition(bitmap, skeleton.Joints[JointType.HipCenter]);
            var hipleft = CalculateJointPosition(bitmap, skeleton.Joints[JointType.HipLeft]);
            var hipright = CalculateJointPosition(bitmap, skeleton.Joints[JointType.HipRight]);
            var kneeleft = CalculateJointPosition(bitmap, skeleton.Joints[JointType.KneeLeft]);
            var kneeright = CalculateJointPosition(bitmap, skeleton.Joints[JointType.KneeRight]);
            var ankleleft = CalculateJointPosition(bitmap, skeleton.Joints[JointType.AnkleLeft]);
            var ankleright = CalculateJointPosition(bitmap, skeleton.Joints[JointType.AnkleRight]);

            gobject.DrawEllipse(pen, new Rectangle((int)head.X - 25, (int)head.Y - 25, 50, 50));
            gobject.DrawEllipse(pen, new Rectangle((int)neck.X - 5, (int)neck.Y, 10, 10));
            gobject.DrawLine(pen, head.X, head.Y + 25, neck.X, neck.Y);

            gobject.DrawLine(pen, neck.X, neck.Y, rightshoulder.X, rightshoulder.Y);
            gobject.DrawLine(pen, neck.X, neck.Y, leftshoulder.X, leftshoulder.Y);
            gobject.DrawLine(_armr, rightshoulder.X, rightshoulder.Y, rightelbow.X, rightelbow.Y);
            gobject.DrawLine(_arml, leftshoulder.X, leftshoulder.Y, leftelbow.X, leftelbow.Y);

            gobject.DrawLine(pen, rightshoulder.X, rightshoulder.Y, hipcenter.X, hipcenter.Y);
            gobject.DrawLine(pen, leftshoulder.X, leftshoulder.Y, hipcenter.X, hipcenter.Y);

            gobject.DrawEllipse(pen, new Rectangle((int)rightwrist.X - 10, (int)rightwrist.Y - 10, 20, 20));
            gobject.DrawLine(_armr, rightelbow.X, rightelbow.Y, rightwrist.X, rightwrist.Y);
            gobject.DrawEllipse(pen, new Rectangle((int)leftwrist.X - 10, (int)leftwrist.Y - 10, 20, 20));
            gobject.DrawLine(_arml, leftelbow.X, leftelbow.Y, leftwrist.X, leftwrist.Y);

            gobject.DrawLine(pen, hipcenter.X, hipcenter.Y, hipleft.X, hipleft.Y);
            gobject.DrawLine(pen, hipcenter.X, hipcenter.Y, hipright.X, hipright.Y);
            gobject.DrawLine(pen, hipleft.X, hipleft.Y, kneeleft.X, kneeleft.Y);
            gobject.DrawLine(pen, hipright.X, hipright.Y, kneeright.X, kneeright.Y);
            gobject.DrawLine(pen, kneeright.X, kneeright.Y, ankleright.X, ankleright.Y);
            gobject.DrawLine(pen, kneeleft.X, kneeleft.Y, ankleleft.X, ankleleft.Y);

            return bitmap;
        }


        /// <summary>
        /// This method turns a skeleton joint position vector into one that is scaled to the depth image
        /// </summary>
        /// <param name="bitmap"></param>
        /// <param name="joint"></param>
        /// <returns></returns>
        protected SkeletonPoint CalculateJointPosition(Bitmap bitmap, Joint joint)
        {
            var jointx = joint.Position.X;
            var jointy = joint.Position.Y;
            var jointz = joint.Position.Z;

            if (jointz < 1)
                jointz = 1;

            var jointnormx = jointx / (jointz * 1.1f);
            var jointnormy = -(jointy / jointz * 1.1f);
            var point = new SkeletonPoint();
            point.X = (jointnormx + 0.5f) * bitmap.Width;
            point.Y = (jointnormy + 0.5f) * bitmap.Height;
            return point;
        }



        protected void GetWaitingMessages(Dictionary<int, GestureMapState> gestureMapDict)
        {
            foreach (var map in _gestureMaps)
            {
                if (map.Value.MessagesWaiting)
                {
                    foreach (var msg in map.Value.Messages)
                    {
                        rtbMessages.AppendText(msg + "\r");
                        rtbMessages.ScrollToCaret();
                    }
                    map.Value.Messages.Clear();
                    map.Value.MessagesWaiting = false;
                }
            }
        }


        void SensorDepthFrameReady(AllFramesReadyEventArgs e)
        {
            // if the window is displayed, show the depth buffer image
            if (WindowState != FormWindowState.Minimized)
            {
                using (var frame = e.OpenDepthImageFrame())
                {
                    _bitmap = CreateBitMapFromDepthFrame(frame);
                }
            }
        }


        private Bitmap CreateBitMapFromDepthFrame(DepthImageFrame frame)
        {
            if (frame != null)
            {
                var bitmapImage = new Bitmap(frame.Width, frame.Height, PixelFormat.Format16bppRgb565);
                var g = Graphics.FromImage(bitmapImage);
                g.Clear(Color.FromArgb(0, 34, 68));
                return bitmapImage;
            }
            return null;
        }

        private void rtbMessages_TextChanged(object sender, EventArgs e)
        {

        }

        private void video_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
