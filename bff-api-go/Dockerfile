# build stage
FROM golang:1.21-alpine AS build

WORKDIR /app

COPY go.mod go.sum ./
RUN go mod download

COPY . .

RUN go mod tidy
RUN go build -o /bff-api-go

# final stage
FROM alpine:latest

WORKDIR /

COPY --from=build /bff-api-go /bff-api-go

EXPOSE 8081

CMD ["/bff-api-go"]
