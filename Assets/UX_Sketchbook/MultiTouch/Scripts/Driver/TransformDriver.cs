using System;
using System.Collections;
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
        [SerializeField]
        InputSource m_InputSource = null;

        [SerializeField]
        Transform m_TargetTransform = null;

        [SerializeField]
        float m_TransformSpeed = 10f;

        [SerializeField]
        float m_PanTransformSpeed = 10f;

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

        }

        Vector3 ComputeInputCentroid()
        {
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
        }

        void ProcessInputs()
        {
            int numberOfInputs = m_InputSource.NumberOfActiveInputs;

            /*
            if (numberOfInputs == 1)
            {

                return;
            }
            */
            if (numberOfInputs == 2)
            {
                MixedRealityPose inputCentroid = new MixedRealityPose(ComputeInputCentroid());

                Vector3 newMoveTarget = moveLogic.Update(inputCentroid, m_TargetTransform.rotation, m_TargetTransform.localScale, false);
                Vector3 panDelta = -(newMoveTarget - m_ObjectStartPose.Position) * m_PanTransformSpeed;

                Quaternion newRotTarget = m_TargetTransform.rotation;
                if (m_AllowRollGesture)
                {
                    rotationLogic.Update(InputArray);
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

                float newScaleDistance = newScaleRatio;
                Vector3 scaleDirection = m_ObjectStartPose.Rotation * Vector3.forward * newScaleDistance;
                Vector3 targetPosition = m_ObjectStartPose.Position + panDelta + scaleDirection;

                ComputeInertialParameters(targetPosition, newRotTarget);
            }
          
            DegradeInertialParameters();
            m_TargetPosition = m_TargetTransform.position + m_DeltaPosition;
            m_TargetRotation = m_TargetTransform.rotation * m_DeltaRotation;
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
            m_TargetTransform.position = Vector3.Lerp(m_TargetTransform.position, m_TargetPosition, Time.deltaTime * 8f * m_TransformSpeed * m_TransformDecayFactor);
            m_TargetTransform.rotation = Quaternion.Slerp(m_TargetTransform.rotation, m_TargetRotation, Time.deltaTime * 8f * m_RotationSmoothingFactor * m_TransformDecayFactor);
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