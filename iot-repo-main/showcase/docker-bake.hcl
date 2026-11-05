group "default" {
  targets = ["mqtt", "backend", "frontend"]
}

target "mqtt" {
  context = "."
  dockerfile = "mqtt/Dockerfile"
  tags = ["andreasfrost1004/luggagetracker-showcase-mqtt:latest"]
  platforms = ["linux/amd64", "linux/arm64"]
}

target "backend" {
  context = "."
  dockerfile = "backend/Dockerfile"
  tags = ["andreasfrost1004/luggagetracker-showcase-backend:latest"]
  platforms = ["linux/amd64", "linux/arm64"]
}

target "frontend" {
  context = "./frontend"
  dockerfile = "Dockerfile"
  tags = ["andreasfrost1004/luggagetracker-showcase-frontend:latest"]
  platforms = ["linux/amd64", "linux/arm64"]
}
