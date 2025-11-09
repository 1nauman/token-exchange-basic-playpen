# Project Overview

This project is a secure microservices playpen demonstrating the **Token Exchange** and **Backend for Frontend (BFF)** patterns. It uses Docker Compose to orchestrate a set of services, including Keycloak as an Identity Provider, Envoy as an API Gateway, and several ASP.NET microservices.

The architecture is designed to showcase a secure and scalable microservices setup. An external token from Keycloak is exchanged at the Envoy API Gateway for a trusted internal token. This internal token is then used by a dedicated BFF service to orchestrate calls to downstream microservices, aggregating their responses into a single payload.

## Core Technologies

*   **Orchestration:** Docker Compose
*   **Identity Provider (IdP):** Keycloak
*   **API Gateway:** Envoy Proxy
*   **Token Exchange Service:** ASP.NET Minimal API
*   **BFF Service:** ASP.NET Minimal API
*   **Upstream Microservices:** ASP.NET Minimal APIs (`product-api`, `inventory-api`)

## Architecture Flow

1.  A client sends a request to the Envoy API Gateway with an external JWT from Keycloak.
2.  Envoy validates the external JWT.
3.  Envoy calls the `token-exchanger` service to exchange the external JWT for an internal JWT.
4.  A Lua script in Envoy replaces the external token with the internal token in the `Authorization` header.
5.  Envoy routes the request to the `bff-api` service.
6.  The `bff-api` service validates the internal JWT.
7.  The `bff-api` makes parallel calls to the `product-api` and `inventory-api`, propagating the internal JWT.
8.  The `bff-api` aggregates the responses and returns a single JSON object to the client.

# Building and Running

## Prerequisites

*   Docker and Docker Compose

## Instructions

1.  **Build and Run the Stack:**
    From the root directory, run the following command:
    ```bash
    docker compose up --build --detach
    ```
    This will start all the services in detached mode.

2.  **Verify the Services:**
    You can check the logs of the services to ensure they are running correctly:
    ```bash
    docker compose logs bff-api
    docker compose logs product-api
    docker compose logs inventory-api
    ```

# Development Conventions

*   **Microservices:** The application is structured as a set of microservices, each with its own responsibility.
*   **ASP.NET Minimal APIs:** The microservices are built using ASP.NET Minimal APIs, which are lightweight and suitable for this type of architecture.
*   **Docker:** All services are containerized using Docker, and the entire application is orchestrated with Docker Compose.
*   **Configuration:** Configuration for each service is provided through `appsettings.json` files. Docker Compose is used to manage environment-specific settings and service-to-service communication.
*   **Security:** Security is a key focus of this project, with token exchange and JWT validation being central to the architecture.
