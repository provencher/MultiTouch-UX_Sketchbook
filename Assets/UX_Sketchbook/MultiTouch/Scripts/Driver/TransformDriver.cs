//------------------------------------------------------------------------------ -
//MultiTouch-UX_Sketchbook
//https://github.com/provencher/MultiTouch-UX_Sketchbook
//------------------------------------------------------------------------------ -
//
//MIT License
//
//Copyright(c) 2020 Eric Provencher
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files(the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions :
//
//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
//------------------------------------------------------------------------------ -

using System.Collections.Generic;
using System.Linq;
using Microsoft.MixedReality.Toolkit.Physics;
using Microsoft.MixedReality.Toolkit.Utilities;
using prvncher.UX_Sketchbook.MultiTouch.Input;
using UnityEngine;

namespace prvncher.UX_Sketchbook.MultiTouch.Driver
{
    public class TransformDriver : MonoBehaviour
    {
        public enum GestureType
        {
            None,
            Single,
            Multi
        }

        GestureType m_CurrentGestureType = GestureType.None;
        
        [SerializeField]
        InputSource m_InputSource = null;

        [SerializeField]
        Transform m_TargetTransform = null;

        [SerializeField]
        float m_TransformSpeed = 10f;

        [SerializeField]
        float m_PanTransformSpeed = 10f;

        [SerializeField]
        float m_ScaleGestureDistance = 1f;

        [SerializeField]
        float m_RotationSmoothingFactor = 0.1f;

        [SerializeField]
        float m_TransformDecayTime = 2f;

        [SerializeField]
        bool m_AllowRollGesture = true;

        ManipulationMoveLogic moveLogic = new ManipulationMoveLogic();
        TwoHandRotateLogic rotationLogic = new TwoHandRotateLogic();
        TwoHandScaleLogic scaleLogic = new TwoHandScaleLogic();

        private Vector3[] InputArray => m_InputSource.FingerPositions.ToArray();

        MixedRealityPose m_TwoFingerTouchStartCentroid;
        MixedRealityPose m_ObjectStartPose;

        Vector3 m_TargetPosition = Vector3.zero;
        Quaternion m_TargetRotation = Quaternion.identity;

        Vector3 m_VelocityDirection = Vector3.zero;
        Vector3 m_AngularVelocityDirection = Vector3.zero;

        Vector3 m_DeltaPosition = Vector3.zero;
        Quaternion m_DeltaRotation = Quaternion.identity;

        const int c_VelocitySampleLimit = 10;
        Queue<Vector3> m_VelocityDirectionSamples = new Queue<Vector3>(c_VelocitySampleLimit);

        float m_TransformDecayFactor = 1f;

        void Awake()
        {
            if (m_InputSource == null)
            {
                Debug.LogError("No Input source found. Destroying driver");
                Destroy(this);
                return;
            }

            if (m_TargetTransform == null)
            {
                m_TargetTransform = transform;
                Debug.Log("No target transform assigned, driving this transform.");
            }
        }

        void OnEnable()
        {
            m_InputSource.OnOneFingerGestureStarted += OnOneFingerGestureStarted;
            m_InputSource.OnTwoFingerGestureStarted += OnTwoFingerGestureStarted;
            m_ObjectStartPose = new MixedRealityPose(m_TargetTransform.position, m_TargetTransform.rotation);
        }

        void OnDisable()
        {
            m_InputSource.OnOneFingerGestureStarted -= OnOneFingerGestureStarted;
            m_InputSource.OnTwoFingerGestureStarted -= OnTwoFingerGestureStarted;
        }

        void Update()
        {
            ProcessInputs();
            UpdateTransform();
        }

        void OnOneFingerGestureStarted()
        {
            if(m_CurrentGestureType != GestureType.None)
            //if (m_CurrentTransformDecayTime >= m_TransformDecayTime - 0.05f)
            {
                return;
            }
            
            m_TwoFingerTouchStartCentroid = new MixedRealityPose(ComputeInputCentroid());
            m_ObjectStartPose = new MixedRealityPose(m_TargetTransform.position, m_TargetTransform.rotation);

            moveLogic.Setup(m_TwoFingerTouchStartCentroid, m_TwoFingerTouchStartCentroid.Position, m_ObjectStartPose, m_TargetTransform.localScale);

            m_VelocityDirectionSamples.Clear();
            m_CurrentGestureType = GestureType.Single;
        }

        Vector3 ComputeInputCentroid()
        {
            if (m_InputSource.FingerPositions.Count == 0)
            {
                return Vector3.zero;
            }
            if (m_InputSource.FingerPositions.Count == 1)
            {
                return m_InputSource.FingerPositions[0];
            }
            Vector3 poseAverage = Vector3.zero;
            foreach (var position in m_InputSource.FingerPositions)
            {
                poseAverage += position;
            }
            poseAverage /= m_InputSource.FingerPositions.Count;
            return poseAverage;
        }

        void OnTwoFingerGestureStarted()
        {
            m_TwoFingerTouchStartCentroid = new MixedRealityPose(ComputeInputCentroid());
            m_ObjectStartPose = new MixedRealityPose(m_TargetTransform.position, m_TargetTransform.rotation);

            moveLogic.Setup(m_TwoFingerTouchStartCentroid, m_TwoFingerTouchStartCentroid.Position, m_ObjectStartPose, m_TargetTransform.localScale);
            rotationLogic.Setup(InputArray, m_TargetTransform);
            scaleLogic.Setup(InputArray, m_TargetTransform);
            
            m_VelocityDirectionSamples.Clear();
            m_CurrentGestureType = GestureType.Multi;
        }


        float m_Sensitivity = 90f;
        void ProcessInputs()
        {
            int numberOfInputs = m_InputSource.NumberOfActiveInputs;
            if (numberOfInputs == 1 && m_CurrentGestureType == GestureType.Single)
            {
                MixedRealityPose inputCentroid = new MixedRealityPose(ComputeInputCentroid());
                Vector3 displacementDelta = inputCentroid.Position - m_TwoFingerTouchStartCentroid.Position;
                
                //displacementDelta = Quaternion.Inverse(m_TargetTransform.rotation) * displacementDelta;
                /*
                float xAngle = Mathf.Repeat(-displacementDelta.x * m_Sensitivity, 360f);
                float yAngle = Mathf.Repeat(-displacementDelta.y * m_Sensitivity, 360f);

                Quaternion deltaRot = Quaternion.Euler(yAngle, xAngle, 0f);
                Quaternion newRotTarget = m_ObjectStartPose.Rotation * deltaRot;

        
                
                m_CurrentTransformDecayTime = m_TransformDecayTime;
                */

                Vector3 newMoveTarget = moveLogic.Update(inputCentroid, m_TargetTransform.rotation, m_TargetTransform.localScale, false);
                Vector3 panDelta = -(newMoveTarget - m_ObjectStartPose.Position) * m_PanTransformSpeed;

                m_DeltaPosition = panDelta;
                m_DeltaRotation = Quaternion.identity;
            }
            if (numberOfInputs == 2 && m_CurrentGestureType == GestureType.Multi)
            {
                MixedRealityPose inputCentroid = new MixedRealityPose(ComputeInputCentroid());

                Quaternion newRotTarget = m_TargetTransform.rotation;
                if (m_AllowRollGesture)
                {
                    newRotTarget = rotationLogic.Update(InputArray);
                }

                float newScaleRatio = scaleLogic.GetScaleRatioMultiplier(InputArray);
                if (newScaleRatio < 1)
                {
                    // Invert scale factor and make it negative to move in reverse
                    newScaleRatio = Mathf.Clamp(-((1 / newScaleRatio) - 1), -5f, 0f);
                }
                else
                {
                    newScaleRatio = Mathf.Clamp(newScaleRatio - 1, 0f, 5f);
                }

                float newScaleDistance = newScaleRatio * m_ScaleGestureDistance;
                Vector3 scaleDirection = m_ObjectStartPose.Rotation * Vector3.forward * newScaleDistance;
                Vector3 targetPosition = m_ObjectStartPose.Position + scaleDirection;

                m_DeltaPosition = targetPosition - m_ObjectStartPose.Position;
                m_DeltaRotation = newRotTarget * Quaternion.Inverse(m_ObjectStartPose.Rotation);

                //ComputeInertialParameters(targetPosition, newRotTarget);
            }

            if (numberOfInputs == 0)
            {
                m_CurrentGestureType = GestureType.None;
            }

            DegradeInertialParameters();
            m_TargetPosition = m_TargetTransform.position + m_DeltaPosition;
            m_TargetRotation = m_ObjectStartPose.Rotation * m_DeltaRotation;
        }

        void AddVelocityDirectionSample(Vector3 newSample)
        {
            m_VelocityDirectionSamples.Enqueue(newSample);

            // Clear old samples
            while (m_VelocityDirectionSamples.Count > c_VelocitySampleLimit)
            {
                m_VelocityDirectionSamples.Dequeue();
            }
        }

        Vector3 GetAverageVelocityDirection()
        {
            if (m_VelocityDirectionSamples.Count == 0)
                return Vector3.zero;

            Vector3 average = Vector3.zero;
            foreach (var sample in m_VelocityDirectionSamples)
            {
                average += sample;
            }
            return average / m_VelocityDirectionSamples.Count;
        }

        void UpdateTransform()
        {
            float smoothAmt = Mathf.SmoothStep(0f, 1f, Time.deltaTime * 8f);
            m_TargetTransform.position = Vector3.Lerp(m_TargetTransform.position, m_TargetPosition, Time.deltaTime * m_TransformSpeed * m_TransformDecayFactor);
            m_TargetTransform.rotation = Quaternion.Slerp(m_TargetTransform.rotation, m_TargetRotation, Time.deltaTime * m_TransformSpeed * m_RotationSmoothingFactor * m_TransformDecayFactor);
            if (!m_AllowRollGesture)
            {
                Vector3 newEulerAngles = m_TargetTransform.rotation.eulerAngles;
                newEulerAngles.z = 0f;
                m_TargetTransform.rotation = Quaternion.Euler(newEulerAngles);
            }
        }

        void ComputeInertialParameters(Vector3 newMoveTarget, Quaternion newRotTarget)
        {
            Vector3 deltaMovePos = newMoveTarget - m_TargetTransform.position;
            Quaternion deltaRot = newRotTarget * Quaternion.Inverse(m_TargetTransform.rotation);

            m_DeltaPosition = deltaMovePos;
            m_DeltaRotation = deltaRot;
            AddVelocityDirectionSample(deltaMovePos);

            m_CurrentTransformDecayTime = m_TransformDecayTime;

            /*
            // Compute translational velocity for inertia
            m_VelocityDirection = (m_TargetPosition - m_TargetTransform.position) * m_TransformSpeed;
            AddVelocityDirectionSample(m_VelocityDirection);

            // Compute Rotational velocity for inertia
            // TODO use this logic go figure out angular momentum for scroll inertia
            m_TargetRotation.ToAngleAxis(out var angleInDegrees, out var rotationAxis);

            Vector3 angularDisplacement = rotationAxis * angleInDegrees * Mathf.Deg2Rad;
            Vector3 angularSpeed = angularDisplacement / Time.deltaTime;

            m_AngularVelocityDirection = angularSpeed;
            Quaternion.AngleAxis(angleInDegrees, rotationAxis);
            */
        }

        float m_CurrentTransformDecayTime = 1;
        void DegradeInertialParameters()
        {
            m_CurrentTransformDecayTime -= Time.deltaTime;
            m_TransformDecayFactor = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m_CurrentTransformDecayTime / m_TransformDecayTime));
            //m_VelocityDirection = Vector3.Lerp(m_VelocityDirection, Vector3.zero, smoothAmt);
            // TODO degrade angular velocity
        }
    }
}