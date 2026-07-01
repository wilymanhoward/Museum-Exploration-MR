using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandModalityForcer : MonoBehaviour
{
    [Header("Controllers")]
    public GameObject leftController;
    public GameObject rightController;

    [Header("Hands")]
    public GameObject leftHand;
    public GameObject rightHand;

    private XRHandSubsystem m_HandSubsystem;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();

    private void Update()
    {
        // 1. Find the running XRHandSubsystem if not cached
        if (m_HandSubsystem == null || !m_HandSubsystem.running)
        {
            m_HandSubsystem = null;
            SubsystemManager.GetSubsystems(s_Subsystems);
            for (int i = 0; i < s_Subsystems.Count; i++)
            {
                if (s_Subsystems[i].running)
                {
                    m_HandSubsystem = s_Subsystems[i];
                    break;
                }
            }
        }

        // 2. Query tracking state and switch GameObjects
        if (m_HandSubsystem != null && m_HandSubsystem.running)
        {
            bool leftHandTracked = m_HandSubsystem.leftHand.isTracked;
            bool rightHandTracked = m_HandSubsystem.rightHand.isTracked;

            if (leftHand != null && leftController != null)
            {
                // Force hand visuals active if tracked, otherwise show controller
                if (leftHand.activeSelf != leftHandTracked) leftHand.SetActive(leftHandTracked);
                if (leftController.activeSelf != !leftHandTracked) leftController.SetActive(!leftHandTracked);
            }

            if (rightHand != null && rightController != null)
            {
                // Force hand visuals active if tracked, otherwise show controller
                if (rightHand.activeSelf != rightHandTracked) rightHand.SetActive(rightHandTracked);
                if (rightController.activeSelf != !rightHandTracked) rightController.SetActive(!rightHandTracked);
            }
        }
        else
        {
            // Fallback: If Hand Tracking Subsystem is not running, controllers are active
            if (leftController != null && !leftController.activeSelf) leftController.SetActive(true);
            if (leftHand != null && leftHand.activeSelf) leftHand.SetActive(false);

            if (rightController != null && !rightController.activeSelf) rightController.SetActive(true);
            if (rightHand != null && rightHand.activeSelf) rightHand.SetActive(false);
        }
    }
}
