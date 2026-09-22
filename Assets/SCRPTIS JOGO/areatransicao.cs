using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class areatransicao : MonoBehaviour
{
    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private Collider2D newBounds;

    private void Awake()
    {
        if (confiner == null)
            confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        if (confiner != null && !confiner.enabled)
            confiner.enabled = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")
            && other.GetComponentInParent<Controlapersonagem>() == null) return;
        if (confiner == null || newBounds == null) return;
        confiner.m_BoundingShape2D = newBounds;
        confiner.InvalidateCache();
    }
}
