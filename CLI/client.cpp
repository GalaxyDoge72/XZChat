
// This shit basically abandoned at this point, can't figure out how to do images with a fucking CLI. //

#include <iostream>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <string>
#include <thread>
#include <openssl/sha.h>
#include <iomanip> // Include this header for std::setfill and std::setw
#include <sstream> // Add this include to resolve incomplete type error for std::ostringstream

#pragma comment(lib, "ws2_32.lib")

const int PORT = 3708;
const int BUFFER_SIZE = 1024;

bool running = true;

using namespace std;

// Forward declaration of CALC_SHA256
string CALC_SHA256(const string& input);

void receive_messages(SOCKET sock) {
    char buffer[BUFFER_SIZE];
    int valread;
    while (running) {
        valread = recv(sock, buffer, BUFFER_SIZE - 1, 0);
        if (valread > 0) {
            buffer[valread] = '\0';
            string received(buffer);
            size_t sep = received.rfind('|');
            if (sep != string::npos) {
                string msg = received.substr(0, sep);
                string hash = received.substr(sep + 1);
                if (CALC_SHA256(msg) == std::string(hash)) {
                    cout << "\r" << msg << " | [HASH OK]                                " << endl;
                } else {
                    cout << "\rMessage corrupted!                                " << endl;
                }
            } else {
                cout << "\rMalformed message!                                " << endl;
            }
            cout << "Message: " << flush;
        }
        else {
            cout << "\rServer disconnected." << endl;
            running = false;
            break;
        }
    }
}

string CALC_SHA256(const string& input) {
    unsigned char hash[SHA256_DIGEST_LENGTH];
    SHA256((const unsigned char*)input.c_str(), input.length(), hash);
    ostringstream os{};
    os << std::hex << std::setfill('0');
    for (int i = 0; i < SHA256_DIGEST_LENGTH; ++i) {
        os << std::setw(2) << static_cast<int>(hash[i]);
    }
    return os.str(); 
}

int main() {
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        cerr << "WSAStartup failed" << endl;
        return 1;
    }

    SOCKET sock = INVALID_SOCKET;
    if ((sock = socket(AF_INET, SOCK_STREAM, 0)) == INVALID_SOCKET) {
        cerr << "Socket creation error: " << WSAGetLastError() << endl;
        WSACleanup();
        return 1;
    }

    struct sockaddr_in serv_addr;
    serv_addr.sin_family = AF_INET;
    serv_addr.sin_port = htons(PORT);
    cout << "Enter the IP address of the server: ";
    string IP_ADDR;
    cin >> IP_ADDR;
    cin.ignore(); 

    if (inet_pton(AF_INET, IP_ADDR.c_str(), &serv_addr.sin_addr) <= 0) {
        cerr << "Invalid address/ Address not supported" << endl;
        closesocket(sock);
        WSACleanup();
        return 1;
    }

    if (connect(sock, (struct sockaddr*)&serv_addr, sizeof(serv_addr)) < 0) {
        cerr << "Connection Failed: " << WSAGetLastError() << endl;
        closesocket(sock);
        WSACleanup();
        return 1;
    }

    string username;
    cout << "Enter your username: ";
    getline(cin, username);
    if (username.empty()) {
        username = "Anonymous";
    }

    // Format the username command and send it
    string nick_command = "/nick " + username;
    if (send(sock, nick_command.c_str(), nick_command.length(), 0) == SOCKET_ERROR) {
        cerr << "Failed to set username: " << WSAGetLastError() << endl;
        closesocket(sock);
        WSACleanup();
        return 1;
    }
    // --- End of username setup ---

    cout << "Welcome to the chat! Type 'exit' to quit." << endl;

    thread receiver_thread(receive_messages, sock);

    string line_to_send;
    while (running) {
        cout << "Message: " << flush;
        if (!getline(cin, line_to_send) || !running) {
            running = false;
            break;
        }

        if (line_to_send == "exit") {
            running = false;
            break;
        }

        // Compute hash and append to message
        string hash = CALC_SHA256(line_to_send);
        string message_with_hash = line_to_send + "|" + hash;

        if (send(sock, message_with_hash.c_str(), message_with_hash.length(), 0) == SOCKET_ERROR) {
            cerr << "Send failed: " << WSAGetLastError() << endl;
            running = false;
            break;
        }
        
    }

    closesocket(sock); // Closing the socket will cause recv in the thread to exit
    if (receiver_thread.joinable()) {
        receiver_thread.join();
    }
    WSACleanup();
    return 0;
}