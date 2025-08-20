using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI : MonoBehaviour
{
    [SerializeField] GameObject networkSetup;

    private void Start()
    {
        networkSetup.SetActive(true);
    }
}
