using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class areatransicao : MonoBehaviour
{
    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private Collider2D newBounds;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            confiner.m_BoundingShape2D = newBounds;
           confiner.InvalidateCache();
        }
    }
}
