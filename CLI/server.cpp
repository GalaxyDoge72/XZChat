#include <iostream>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <vector>
#include <thread>
#include <mutex>
#include <algorithm>
#include <string>
#include <map>
#include <memory>
#include <sstream>
#include <iomanip>
#include <openssl/sha.h>
#include <openssl/evp.h>
#include <chrono>
#include <unordered_map>
#include <set>
#include <atomic>

#pragma comment(lib, "ws2_32.lib")
#pragma comment(lib, "libcrypto.lib")
#pragma comment(lib, "libssl.lib")

const int PORT = 3708;
const int RECV_BUFFER_SIZE = 4096; // Standard buffer size for receiving chunks

// Prefix to identify image data packets
const std::string IMAGE_PREFIX = "IMAGE_DATA:";

// Shared resources protected by a mutex
std::vector<SOCKET> clients;
std::map<SOCKET, std::string> client_usernames;
std::mutex clients_mutex;

std::unordered_map<SOCKET, std::chrono::steady_clock::time_point> last_message_time;
const int MIN_SECONDS_BETWEEN_MESSAGES = 1; // 1 second cooldown

std::set<std::string> banned_usernames;
std::atomic<bool> server_running{true};

std::string CALC_SHA256(const std::string& input);

void broadcast_message(const std::string& message, SOCKET sender_socket) {
    std::lock_guard<std::mutex> lock(clients_mutex);
    std::string hash = CALC_SHA256(message);
    std::string message_with_hash = message + "|" + hash + "\n"; // Append newline delimiter
    for (SOCKET client_socket : clients) {
        if (client_socket != sender_socket) {
            send(client_socket, message_with_hash.c_str(), static_cast<int>(message_with_hash.length()), 0);
        }
    }
}

// Relays a raw data packet (like an image) to all clients except the sender
void relay_raw_packet(const std::string& packet, SOCKET sender_socket) {
    std::lock_guard<std::mutex> lock(clients_mutex);
    std::string packet_with_newline = packet + "\n"; // Ensure newline delimiter is present
    for (SOCKET client_sock : clients) {
        if (client_sock != sender_socket) {
            send(client_sock, packet_with_newline.c_str(), static_cast<int>(packet_with_newline.length()), 0);
        }
    }
}

void handle_client(SOCKET client_socket, const std::string& client_ip) {
    {
        std::lock_guard<std::mutex> lock(clients_mutex);
        clients.push_back(client_socket);
        client_usernames[client_socket] = "Anonymous";
        last_message_time[client_socket] = std::chrono::steady_clock::now() - std::chrono::seconds(MIN_SECONDS_BETWEEN_MESSAGES);
    }

    // Send a welcome message to the newly connected client
    std::string welcome_msg = "Welcome to the server, " + client_ip + "!";
    std::string welcome_msg_with_hash = welcome_msg + "|" + CALC_SHA256(welcome_msg) + "\n";
    send(client_socket, welcome_msg_with_hash.c_str(), static_cast<int>(welcome_msg_with_hash.length()), 0);

    auto recv_buffer = std::make_unique<char[]>(RECV_BUFFER_SIZE);
    std::string accumulated_data;
    int bytes_received = 0;

    // Main receive loop with accumulation buffer
    while ((bytes_received = recv(client_socket, recv_buffer.get(), RECV_BUFFER_SIZE, 0)) > 0) {
        accumulated_data.append(recv_buffer.get(), bytes_received);

        size_t pos;
        while ((pos = accumulated_data.find('\n')) != std::string::npos) {
            std::string message = accumulated_data.substr(0, pos);
            accumulated_data.erase(0, pos + 1); // Erase the processed message and the '\n'

            // --- Anti-spam check ---
            auto now = std::chrono::steady_clock::now();
            {
                std::lock_guard<std::mutex> lock(clients_mutex);
                auto last = last_message_time[client_socket];
                auto elapsed = std::chrono::duration_cast<std::chrono::seconds>(now - last).count();
                if (elapsed < MIN_SECONDS_BETWEEN_MESSAGES) {
                    // Optionally send a warning to the client
                    std::string warn = "Server: Please wait before sending another message.\n";
                    send(client_socket, warn.c_str(), static_cast<int>(warn.length()), 0);
                    continue; // Skip processing this message
                }
                last_message_time[client_socket] = now;
            }
            // --- End anti-spam check ---

            if (message.rfind(IMAGE_PREFIX, 0) == 0) {
                std::string sender_username;
                {
                    std::lock_guard<std::mutex> lock(clients_mutex);
                    sender_username = client_usernames[client_socket];
                }
                std::cout << "[" << sender_username << " from " << client_ip << "] Relaying image data." << std::endl;
                // Relay the raw image packet as-is to other clients
                relay_raw_packet(message, client_socket);
            }

            else {
                size_t sep = message.rfind('|');
                if (sep != std::string::npos && sep < message.length() - 1) {
                    std::string msg_part = message.substr(0, sep);
                    std::string hash_part = message.substr(sep + 1);

                    // Handle nickname changes
                    if (msg_part.rfind("/nick ", 0) == 0) {
                        std::string new_username = msg_part.substr(6);
                        if (banned_usernames.count(new_username)) {
                            std::string msg = "Server: This username is banned.\n";
                            send(client_socket, msg.c_str(), static_cast<int>(msg.length()), 0);
                            closesocket(client_socket);
                            return;
                        }
                        std::string old_username;
                        {
                            std::lock_guard<std::mutex> lock(clients_mutex);
                            old_username = client_usernames[client_socket];
                            client_usernames[client_socket] = new_username;
                        }
                        std::string status_msg = "Server: " + old_username + " is now known as " + new_username;
                        std::cout << status_msg << std::endl;
                        broadcast_message(status_msg, client_socket);
                    }
                    // Handle regular chat messages
                    else {
                        if (CALC_SHA256(msg_part) == hash_part) {
                            std::string sender_username;
                            {
                                std::lock_guard<std::mutex> lock(clients_mutex);
                                sender_username = client_usernames[client_socket];
                            }
                            std::string message_to_broadcast = sender_username + " says: " + msg_part;
                            std::cout << "Broadcasting: " << message_to_broadcast << std::endl;
                            broadcast_message(message_to_broadcast, client_socket);
                        }
                        else {
                            std::cerr << "Corrupted message from " << client_ip << ": Hash Mismatch!" << std::endl;
                        }
                    }
                }
                else {
                    std::cerr << "Malformed message from " << client_ip << ": No hash separator '|'." << std::endl;
                }
            }
        }
    }

    // Handle client disconnection
    std::string disconnected_username;
    {
        std::lock_guard<std::mutex> lock(clients_mutex);
        disconnected_username = client_usernames[client_socket];
        // Remove client from lists
        clients.erase(std::remove(clients.begin(), clients.end(), client_socket), clients.end());
        client_usernames.erase(client_socket);
        last_message_time.erase(client_socket);
    }

    if (bytes_received == 0) {
        std::string disconnect_msg = disconnected_username + " (" + client_ip + ") disconnected.";
        std::cout << disconnect_msg << std::endl;
        broadcast_message(disconnect_msg, INVALID_SOCKET); // Broadcast to all remaining clients
    }
    else {
        std::string error_msg = "recv failed with error " + std::to_string(WSAGetLastError()) + " for client " + client_ip;
        std::cerr << error_msg << std::endl;
        std::string disconnect_msg = disconnected_username + " (" + client_ip + ") disconnected due to an error.";
        broadcast_message(disconnect_msg, INVALID_SOCKET);
    }

    closesocket(client_socket);
}


// Function to calculate SHA256 hash using OpenSSL EVP API
std::string CALC_SHA256(const std::string& input) {
    unsigned char hash[SHA256_DIGEST_LENGTH];
    std::unique_ptr<EVP_MD_CTX, decltype(&EVP_MD_CTX_free)> ctx(EVP_MD_CTX_new(), EVP_MD_CTX_free);

    if (!ctx) {
        throw std::runtime_error("Failed to create EVP_MD_CTX");
    }

    if (EVP_DigestInit_ex(ctx.get(), EVP_sha256(), nullptr) != 1 ||
        EVP_DigestUpdate(ctx.get(), input.c_str(), input.length()) != 1 ||
        EVP_DigestFinal_ex(ctx.get(), hash, nullptr) != 1) {
        throw std::runtime_error("Failed to compute SHA256 hash");
    }

    std::stringstream ss;
    for (int i = 0; i < SHA256_DIGEST_LENGTH; i++) {
        ss << std::hex << std::setw(2) << std::setfill('0') << static_cast<int>(hash[i]);
    }
    return ss.str();
}

// Console command thread function
void console_command_thread() {
    std::string line;
    while (server_running) {
        std::getline(std::cin, line);
        if (line.rfind("/ban ", 0) == 0) {
            std::string username = line.substr(5);
            {
                std::lock_guard<std::mutex> lock(clients_mutex);
                banned_usernames.insert(username);
                // Disconnect all clients with this username
                for (auto it = client_usernames.begin(); it != client_usernames.end(); ++it) {
                    if (it->second == username) {
                        SOCKET sock = it->first;
                        std::string msg = "Server: You have been banned.\n";
                        send(sock, msg.c_str(), static_cast<int>(msg.length()), 0);
                        closesocket(sock);
                        clients.erase(std::remove(clients.begin(), clients.end(), sock), clients.end());
                        last_message_time.erase(sock);

                    }
                }
            }
            std::cout << "User '" << username << "' has been banned." << std::endl;
        }

        else if (line.rfind("/unban ", 0) == 0) {
            std::string username = line.substr(7);
            std::lock_guard<std::mutex> lock(clients_mutex);
            banned_usernames.erase(username);
            std::cout << "User '" << username << "' has been unbanned." << std::endl;
        }
    }
}

int main() {
    WSADATA wsa;
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        std::cerr << "Failed to initialize Winsock: " << WSAGetLastError() << std::endl;
        return 1;
    }

    SOCKET server_fd = socket(AF_INET, SOCK_STREAM, 0);
    if (server_fd == INVALID_SOCKET) {
        std::cerr << "Could not create socket: " << WSAGetLastError() << std::endl;
        WSACleanup();
        return 1;
    }

    sockaddr_in address;
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = INADDR_ANY;
    address.sin_port = htons(PORT);

    if (bind(server_fd, (struct sockaddr*)&address, sizeof(address)) == SOCKET_ERROR) {
        std::cerr << "Bind failed: " << WSAGetLastError() << std::endl;
        closesocket(server_fd);
        WSACleanup();
        return 1;
    }

    if (listen(server_fd, SOMAXCONN) == SOCKET_ERROR) {
        std::cerr << "Listen failed: " << WSAGetLastError() << std::endl;
        closesocket(server_fd);
        WSACleanup();
        return 1;
    }

    std::cout << "Server listening on port " << PORT << ". Ready for connections." << std::endl;

    std::thread(console_command_thread).detach();

    while (true) {
        sockaddr_in client_addr;
        int client_addr_len = sizeof(client_addr);
        SOCKET new_socket = accept(server_fd, (struct sockaddr*)&client_addr, &client_addr_len);
        if (new_socket == INVALID_SOCKET) {
            std::cerr << "Accept failed: " << WSAGetLastError() << std::endl;
            continue;
        }

        char client_ip[INET_ADDRSTRLEN];
        inet_ntop(AF_INET, &client_addr.sin_addr, client_ip, INET_ADDRSTRLEN);

        std::cout << "New connection from " << client_ip << std::endl;

        std::thread client_thread(handle_client, new_socket, std::string(client_ip));
        client_thread.detach();
    }

    closesocket(server_fd);
    WSACleanup();
    return 0;
}