# Wells Chat

Wells Chat is an end-to-end encrypted chat application built using **.NET 8** and **SignalR**. This project contains two independent applications: 
- A console application
- A MAUI application

The console application is a simple chat application that runs in the terminal. The MAUI application is a modern cross-platform chat application that runs on Android, iOS, macOS, and Windows. The project also includes a server application to facilitate communication between the client applications using a SignalR hub.

This open-source project demonstrates the capabilities of **.NET 8**, **MAUI**, **SignalR** and **SyncFusion**, and provides reference implementations for developers who want to build their own end-to-end encrypted chat applications.

## Features

1. **End-to-End Encryption**: All messages are encrypted on the sender's device and can only be decrypted by the chat participants. The server does not have access to the encryption keys and cannot decrypt the messages.

2. **Real-Time Messaging**: Built with SignalR, the app provides real-time, bidirectional communication between the server and the client.

3. **User Authentication**: Both the console app and the MAUI app include user authentication to ensure that only authorized users can access the chat. In this example, only users who are members of a specific Azure Active Directory B2C tenant can access the chat.

4. **Group Chat**: Both applications support group chat with multiple participants in a single chat room.

5. **Cross-Platform**: The MAUI app runs on multiple platforms including Android, iOS, macOS, and Windows with a single codebase.

6. **Swipe Gestures & Context Menus**: The MAUI app demonstrates use of swipe gestures for deleting messages and context menus for quick access to common actions, like copying a message to your clipboard and extracting URLs from messages.

7. **UI Controls**: Along with native XAML controls, the MAUI app incorporates Syncfusion UI components to showcase the capabilities of the Syncfusion Essential UI Kit for MAUI.

8. **Privacy Features**: The MAUI app includes privacy features such as the ability to lock the screen with a password to hide chat messages and clear the local chat history.

## Screenshots
### Desktop Light Mode
![cohoist-chat-screenshot-1](https://github.com/cohoist/cohoist-chat/assets/8410696/22c1ba81-7896-4657-b5ef-5c61730ca44a)

### Desktop Dark Mode
![cohoist-chat-screenshot-3](https://github.com/cohoist/cohoist-chat/assets/8410696/d77e7727-b371-4d58-841b-ae3ba0a9f57a)

### Android Light Mode
![cc_android](https://github.com/cohoist/cohoist-chat/assets/8410696/f74f2027-a33e-4986-9264-eff9604f0c2b)

### Console Application
![cohoist-chat-console-screenshot](https://github.com/cohoist/cohoist-chat/assets/8410696/1d4817b1-070c-43c6-9c23-91c53b7efd33)

### Privacy Features
![cohoist-chat-screenshot-2](https://github.com/cohoist/cohoist-chat/assets/8410696/e602099a-7ec9-463e-b29b-a5cedb957cd9)
![cohoist-chat-screenshot-4](https://github.com/cohoist/cohoist-chat/assets/8410696/5dd216f0-a5f7-4151-8829-ab33346b3a4a)
