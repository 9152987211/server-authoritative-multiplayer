# server-authoritative-multiplayer
A 1v1 fps game I am currently building in Unity that uses a server authoritative architecture. This means that the client only sends inputs to the server, the server then returns the correct state. To prevent input lag, the client uses client side prediction and server rollback to prevent miss-matches between the client and server.
