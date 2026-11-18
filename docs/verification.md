# Verification

The airport simulation and deployment refactor has not been built or run in this workspace. Builds, migrations, and tests were intentionally not started per the requested workflow. Review and run the relevant validation before deployment:

- Build all four images through the `Build and publish` GitHub Actions workflow's validation job.
- Start a clean Compose stack with a configured `.env` and existing `ingress` Docker network.
- Confirm all three default Mosquitto brokers and backend become healthy.
- Run the normal and connecting-flight scenarios; verify a first check-in creates its trip, journey, and assignment and that departure/arrival events reach the dashboard.
- Register an additional airport and confirm its broker joins the private network and bridges to `regional`.
- Confirm stop/status behavior and that only the backend, frontend, and two simulation services have Watchtower enable labels.
