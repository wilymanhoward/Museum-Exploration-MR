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

    private float m_LeftHandUntrackedTimer = 0f;
    private float m_RightHandUntrackedTimer = 0f;
    private const float SwitchDelay = 0.3f; // 300ms delay to prevent rapid controller/hand fighting jitter

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

        // 2. Query tracking state and switch GameObjects with hysteresis
        if (m_HandSubsystem != null && m_HandSubsystem.running)
        {
            bool leftHandTracked = m_HandSubsystem.leftHand.isTracked;
            bool rightHandTracked = m_HandSubsystem.rightHand.isTracked;

            if (leftHand != null && leftController != null)
            {
                if (leftHandTracked)
                {
                    m_LeftHandUntrackedTimer = 0f;
                    if (!leftHand.activeSelf) leftHand.SetActive(true);
                    if (leftController.activeSelf) leftController.SetActive(false);
                }
                else
                {
                    m_LeftHandUntrackedTimer += Time.deltaTime;
                    if (m_LeftHandUntrackedTimer >= SwitchDelay)
                    {
                        if (leftHand.activeSelf) leftHand.SetActive(false);
                        if (!leftController.activeSelf) leftController.SetActive(true);
                    }
                }
            }

            if (rightHand != null && rightController != null)
            {
                if (rightHandTracked)
                {
                    m_RightHandUntrackedTimer = 0f;
                    if (!rightHand.activeSelf) rightHand.SetActive(true);
                    if (rightController.activeSelf) rightController.SetActive(false);
                }
                else
                {
                    m_RightHandUntrackedTimer += Time.deltaTime;
                    if (m_RightHandUntrackedTimer >= SwitchDelay)
                    {
                        if (rightHand.activeSelf) rightHand.SetActive(false);
                        if (!rightController.activeSelf) rightController.SetActive(true);
                    }
                }
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
