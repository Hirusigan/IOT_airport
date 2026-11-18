variable "DOCKERHUB_USERNAME" {
  default = "andreasfrost1004"
}

group "default" {
  targets = ["bootstrap", "backend", "frontend"]
}

target "bootstrap" {
  context = "."
  dockerfile = "docker/bootstrap.Dockerfile"
  tags = ["${DOCKERHUB_USERNAME}/luggagetracker-bootstrap:latest"]
  platforms = ["linux/amd64", "linux/arm64"]
}

target "backend" {
  context = "."
  dockerfile = "backend/Dockerfile"
  tags = ["${DOCKERHUB_USERNAME}/luggagetracker-backend:latest"]
  platforms = ["linux/amd64", "linux/arm64"]
}

target "frontend" {
  context = "./frontend"
  dockerfile = "Dockerfile"
  tags = ["${DOCKERHUB_USERNAME}/luggagetracker-frontend:latest"]
  platforms = ["linux/amd64", "linux/arm64"]
}
