# PortIn
Demonstrates the use of a LLM as a computer-use-agent (CUA) to automate a "port in" process (telecommunications number portability) using a mobile embedded web browser.

See the [root readme.md](/readme.md) for additional context.

## Tasks

The app automates the following tasks:
- **Login** - Login with userid/password
- **Account & Zip** - Get billing zip code and account number
- **Transfer PIN** - Locate the transfer PIN generation page
- **Download Bill** - Get bill details and recurring charges

## Usage

1. Configure credentials in the settings (API Key, Login ID, Password, URL)
2. Click the triangle icon to start the plan
3. The first task waits for manual login, then click `>>|` to continue
4. View captured account info via the menu
