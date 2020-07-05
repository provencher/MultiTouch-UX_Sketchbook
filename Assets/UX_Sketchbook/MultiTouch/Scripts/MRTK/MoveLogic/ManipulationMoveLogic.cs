// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.Physics
{
    /// <summary>
    /// Implements a move logic that will move an object based on the initial position of 
    /// the grab point relative to the pointer and relative to the object, and subsequent
    /// changes to the pointer and the object's rotation
    /// 
    /// Usage:
    /// When a manipulation starts, call Setup.
    /// Call Update any time to update the move logic and get a new rotation for the object.
    /// </summary>
    public class ManipulationMoveLogic
    {
        private Vector3 pointerLocalGrabPoint;
        private Vector3 objectLocalGrabPoint;
        private Vector3 grabToObject;

        /// <summary>
        /// Setup function
        /// </summary>
        public void Setup(MixedRealityPose pointerCentroidPose, Vector3 grabCentroid, MixedRealityPose objectPose, Vector3 objectScale)
        {
            Quaternion worldToPointerRotation = Quaternion.Inverse(pointerCentroidPose.Rotation);
            pointerLocalGrabPoint = worldToPointerRotation * (grabCentroid - pointerCentroidPose.Position);

            objectLocalGrabPoint = Quaternion.Inverse(objectPose.Rotation) * (grabCentroid - objectPose.Position);
            objectLocalGrabPoint = objectLocalGrabPoint.Div(objectScale);

            grabToObject = objectPose.Position - grabCentroid;
        }

        /// <summary>
        /// Update the position based on input.
        /// </summary>
        /// <returns>A Vector3 describing the desired position</returns>
        public Vector3 Update(MixedRealityPose pointerCentroidPose, Quaternion objectRotation, Vector3 objectScale, bool usePointerRotation)
        {
            if (usePointerRotation)
            {
                Vector3 scaledGrabToObject = Vector3.Scale(objectLocalGrabPoint, objectScale);
                Vector3 adjustedPointerToGrab = pointerLocalGrabPoint;
                adjustedPointerToGrab = pointerCentroidPose.Rotation * adjustedPointerToGrab;

                return adjustedPointerToGrab - objectRotation * scaledGrabToObject + pointerCentroidPose.Position;
            }
            else
            {
                return pointerCentroidPose.Position + (pointerCentroidPose.Rotation * pointerLocalGrabPoint + grabToObject);
            }
        }
    }
}
