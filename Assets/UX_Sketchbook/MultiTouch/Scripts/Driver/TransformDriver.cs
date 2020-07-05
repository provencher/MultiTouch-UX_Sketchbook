using System.Collections;
using System.Collections.Generic;
using Microsoft.MixedReality.Toolkit.Physics;
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

        ManipulationMoveLogic moveLogic = new ManipulationMoveLogic();
        TwoHandRotateLogic rotationLogic = new TwoHandRotateLogic();
        TwoHandScaleLogic scaleLogic = new TwoHandScaleLogic();

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

        void OnOneFingerGestureStarted()
        {

        }

        void OnTwoFingerGestureStarted()
        {

        }

        void ProcessInputs()
        {
            int numberOfInputs = m_InputSource.NumberOfActiveInputs;

            if (numberOfInputs == 1)
            {

                return;
            }
            if (numberOfInputs == 2)
            {

                return;
            }
        }

        void UpdateTransform()
        {

        }

        void Update()
        {
            ProcessInputs();
            UpdateTransform();
        }
    }
}