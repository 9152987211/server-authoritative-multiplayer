using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class NetworkSetup : MonoBehaviour
{
    [SerializeField] Camera spectatorCamera;

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        gameObject.SetActive(false);
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        gameObject.SetActive(false);
    }

    public void StartServer()
    {
        NetworkManager.Singleton.StartServer();
        spectatorCamera.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }
}
