using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;

namespace VR_KinectBodyMeter
{

    /// <summary>
    /// Provides measure functionality on skeletal data.
    /// </summary>
    public static class SkeletMeter
    {

        public static double Height(this Skeleton skeleton)
        {
            const double HEAD_OFFSET = 0.14;
            return UpperBodyLength(skeleton) + LegLength(skeleton) + HEAD_OFFSET;
        }

        public static double UpperBodyLength(this Skeleton skeleton)
        {

            var head = skeleton.Joints[JointType.Head];
            var neck = skeleton.Joints[JointType.ShoulderCenter];
            var spine = skeleton.Joints[JointType.Spine];
            var waist = skeleton.Joints[JointType.HipCenter];

            return Length(head, neck, spine, waist);
        }

        public static double LegLength(this Skeleton skeleton)
        {
            var hipLeft = skeleton.Joints[JointType.HipLeft];
            var hipRight = skeleton.Joints[JointType.HipRight];
            var kneeLeft = skeleton.Joints[JointType.KneeLeft];
            var kneeRight = skeleton.Joints[JointType.KneeRight];
            var ankleLeft = skeleton.Joints[JointType.AnkleLeft];
            var ankleRight = skeleton.Joints[JointType.AnkleRight];
            var footLeft = skeleton.Joints[JointType.FootLeft];
            var footRight = skeleton.Joints[JointType.FootRight];

            // Find which leg is tracked more accurately.
            int legLeftTrackedJoints = NumberOfTrackedJoints(hipLeft, kneeLeft, ankleLeft, footLeft);
            int legRightTrackedJoints = NumberOfTrackedJoints(hipRight, kneeRight, ankleRight, footRight);

            double legLength = legLeftTrackedJoints > legRightTrackedJoints ? Length(hipLeft, kneeLeft, ankleLeft, footLeft) : Length(hipRight, kneeRight, ankleRight, footRight);

            return legLength;
        }

        public static double ArmLength(this Skeleton skeleton)
        {
            var sholderLeft = skeleton.Joints[JointType.ShoulderLeft];
            var sholderRight = skeleton.Joints[JointType.ShoulderRight];
            var elbowLeft = skeleton.Joints[JointType.ElbowLeft];
            var elbowRight = skeleton.Joints[JointType.ElbowRight];
            var wristLeft = skeleton.Joints[JointType.WristLeft];
            var wristRight = skeleton.Joints[JointType.WristRight];

            // Find which arm is tracked more accurately.
            int armLeftTrackedJoints = NumberOfTrackedJoints(sholderLeft, elbowLeft, wristLeft);
            int armRightTrackedJoints = NumberOfTrackedJoints(sholderRight, elbowRight, wristRight);

            double armLength = armLeftTrackedJoints > armRightTrackedJoints ? Length(sholderLeft, elbowLeft, wristLeft) : Length(sholderRight, elbowRight, wristRight);

            return armLength;
        }

        public static double ShoulderBreadth(this Skeleton skeleton)
        {
            var sholderLeft = skeleton.Joints[JointType.ShoulderLeft];
            var sholderRight = skeleton.Joints[JointType.ShoulderRight];

            double shoulderBreadth = Length(sholderRight, sholderLeft);

            return shoulderBreadth;
        }

        public static double Length(Joint p1, Joint p2)
        {
            return Math.Sqrt(
                Math.Pow(p1.Position.X - p2.Position.X, 2) +
                Math.Pow(p1.Position.Y - p2.Position.Y, 2) +
                Math.Pow(p1.Position.Z - p2.Position.Z, 2));
        }

        public static double Length(params Joint[] joints)
        {
            double length = 0;

            for (int index = 0; index < joints.Length - 1; index++)
            {
                length += Length(joints[index], joints[index + 1]);
            }

            return length;
        }


        public static int NumberOfTrackedJoints(params Joint[] joints)
        {
            int trackedJoints = 0;

            foreach (var joint in joints)
            {
                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    trackedJoints++;
                }
            }

            return trackedJoints;
        }

    }
}
