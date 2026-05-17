FROM golang:1.24-alpine AS build

WORKDIR /src
COPY go.mod go.sum* ./
RUN go mod download
COPY . .
RUN CGO_ENABLED=0 GOOS=linux go build -o /app/cloud-storage-service .

FROM alpine:3.21
WORKDIR /app
COPY --from=build /app/cloud-storage-service .
EXPOSE 8082
ENTRYPOINT ["/app/cloud-storage-service"]
