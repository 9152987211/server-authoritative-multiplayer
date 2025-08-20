using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject playerCharacter;
    [SerializeField] private Transform viewmodel;

    private float moveSpeed = 1.0f;
    private float gravity = -9.81f;
    private float velocityY = 0.0f;
    private float jumpHeight = 1.7f;
    private CharacterController cc;

    private float mouseSensitivity = 20.0f;
    private float xRotation = 0f;


    //NETCODE GENERAL
    private NetworkTimer timer;
    private float serverTickRate = 60.0f;
    private int bufferSize = 1024;

    //NETCODE CLIENT
    CircularBuffer<StatePayload> clientStateBuffer;
    CircularBuffer<InputPayload> clientInputBuffer;
    StatePayload lastServerState;
    StatePayload lastProcessedState;

    //NETCODE SERVER
    CircularBuffer<StatePayload> serverStateBuffer;
    Queue<InputPayload> serverInputQueue;
    float reconciliationThreshold = 5.0f;
    Queue<InputPayload> clientInputFrameBuffer = new Queue<InputPayload>();


    private void Awake()
    {
        timer = new NetworkTimer(serverTickRate);
        clientStateBuffer = new CircularBuffer<StatePayload>(bufferSize);
        clientInputBuffer = new CircularBuffer<InputPayload>(bufferSize);
        serverStateBuffer = new CircularBuffer<StatePayload>(bufferSize);
        serverInputQueue = new Queue<InputPayload>();
    }


    public override void OnNetworkSpawn()
    {
        cc = GetComponent<CharacterController>();

        if (!IsOwner)
        {
            playerCamera.gameObject.GetComponent<AudioListener>().enabled = false;
            playerCamera.enabled = false;
            playerCharacter.SetActive(true);
            cc.enabled = false;
        } else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
    }

    private void Update()
    {
        timer.UpdateTimer(Time.deltaTime);

        if (IsClient && IsOwner)
        {
            collectRawInput();
        }

        while (timer.ShouldTick())
        {
            handleClientTick();
            handleServerTick();
        }
    }

    private StatePayload processMovement(InputPayload input)
    {
        move(input.inputVector, input.lookVector);

        return new StatePayload()
        {
            tick = input.tick,
            position = transform.position
        };
    }

    private void handleServerTick()
    {
        if (!IsServer) return;

        int bufferIndex = -1;
        while (serverInputQueue.Count > 0)
        {
            InputPayload inputPayload = serverInputQueue.Dequeue();

            bufferIndex = inputPayload.tick % bufferSize;

            StatePayload statePayload = processMovement(inputPayload);
            serverStateBuffer.Add(statePayload, bufferIndex);
        }

        if (bufferIndex == -1) return;
        SendToClientRpc(serverStateBuffer.Get(bufferIndex));
    }

    [ClientRpc]
    public void SendToClientRpc(StatePayload statePayload)
    {
        if (!IsOwner) return;
        lastServerState = statePayload;
    }

    /*private void handleClientTick()
    {
        if (!IsClient || !IsOwner) return;

        int currentTick = timer.currentTick;
        int bufferIndex = currentTick % bufferSize;

        InputPayload inputPayload = new InputPayload()
        {
            tick = currentTick,
            inputVector = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            lookVector = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"))
        };

        clientInputBuffer.Add(inputPayload, bufferIndex);
        SendToServerRpc(inputPayload);

        StatePayload statePayload = processMovement(inputPayload);
        clientStateBuffer.Add(statePayload, bufferIndex);

        handleServerReconciliation();
    }*/

    private void handleClientTick()
    {
        if (!IsClient || !IsOwner) return;

        List<InputPayload> inputBatch = new List<InputPayload>();

        while (clientInputFrameBuffer.Count > 0)
        {
            var input = clientInputFrameBuffer.Dequeue();
            inputBatch.Add(input);

            int bufferIndex = input.tick % bufferSize;
            clientInputBuffer.Add(input, bufferIndex);

            // Client-side prediction
            StatePayload statePayload = processMovement(input);
            clientStateBuffer.Add(statePayload, bufferIndex);
        }

        if (inputBatch.Count > 0)
        {
            SendInputBatchToServerRpc(inputBatch.ToArray());
        }

        handleServerReconciliation();
    }

    private bool shouldReconcile()
    {
        bool isNewServerState = !lastServerState.Equals(default);
        bool isLastStateUndefinedDifferent = lastProcessedState.Equals(default) || !lastProcessedState.Equals(lastServerState);
        
        return isNewServerState && isLastStateUndefinedDifferent;
    }

    private void reconcileState(StatePayload rewindState)
    {
        cc.enabled = false;
        transform.position = rewindState.position;
        cc.enabled = true;

        if (!rewindState.Equals(lastServerState)) return;
        clientStateBuffer.Add(rewindState, rewindState.tick);

        int tickToReplay = lastServerState.tick;

        while(tickToReplay < timer.currentTick)
        {
            int bufferIndex = tickToReplay % bufferSize;
            StatePayload statePayload = processMovement(clientInputBuffer.Get(bufferIndex));
            clientStateBuffer.Add(statePayload, bufferIndex);
            tickToReplay++;
        }
    }

    private void handleServerReconciliation()
    {
        if (!shouldReconcile()) return;

        float positionError;
        int bufferIndex;
        StatePayload rewindState = default;

        bufferIndex = lastServerState.tick % bufferSize;
        if (bufferIndex - 1 < 0) return;

        rewindState = IsHost ? serverStateBuffer.Get(bufferIndex - 1) : lastServerState;
        positionError = Vector3.Distance(rewindState.position, clientStateBuffer.Get(bufferIndex).position);

        if (positionError > reconciliationThreshold)
        {
            reconcileState(rewindState);
        }

        lastProcessedState = lastServerState;
    }

    /*[ServerRpc]
    public void SendToServerRpc(InputPayload input)
    {
        serverInputQueue.Enqueue(input);
    }*/

    [ServerRpc]
    public void SendInputBatchToServerRpc(InputPayload[] inputs)
    {
        foreach (var input in inputs)
        {
            serverInputQueue.Enqueue(input);
        }
    }

    private void move(Vector2 inputVector, Vector2 lookVector)
    {
        //AIMING
        float mouseX = lookVector.x * mouseSensitivity * timer.minTimeBetweenTicks;
        float mouseY = lookVector.y * mouseSensitivity * timer.minTimeBetweenTicks;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        viewmodel.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);


        //MOVING
        float x = inputVector.x;
        float z = inputVector.y;

        Vector3 move = transform.right * x + transform.forward * z;
        move.Normalize();

        if (cc.isGrounded && velocityY < 0)
        {
            velocityY = -2f;
        }

        if (Input.GetButtonDown("Jump") && cc.isGrounded)
        {
            velocityY = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocityY += gravity * Time.deltaTime;

        Vector3 verticalMove = new Vector3(0, velocityY, 0);

        cc.Move(move * moveSpeed * timer.minTimeBetweenTicks + verticalMove * timer.minTimeBetweenTicks);
    }

    private void collectRawInput()
    {
        int currentTick = timer.currentTick;
        float timestamp = Time.time;

        InputPayload inputPayload = new InputPayload()
        {
            tick = currentTick, // still useful, but timestamp is more accurate
            inputVector = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            lookVector = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")),
            time = timestamp
        };

        // Buffer by order of arrival
        clientInputFrameBuffer.Enqueue(inputPayload);
    }
}


public struct InputPayload : INetworkSerializable
{
    public int tick;
    public Vector3 inputVector;
    public Vector3 lookVector;
    public float time;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref inputVector);
        serializer.SerializeValue(ref lookVector);
    }
}


public struct StatePayload : INetworkSerializable
{
    public int tick;
    public Vector3 position;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref position);
    }
}
