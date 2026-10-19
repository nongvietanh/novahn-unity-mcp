"""Expose an existing local Novahn HTTP endpoint to stdio clients such as Claude Desktop."""
import os
from urllib.parse import urlparse

from fastmcp import Client
from fastmcp.client.transports import StreamableHttpTransport
from fastmcp.server import create_proxy


def main() -> None:
    url = os.environ["NOVAHN_PROXY_URL"]
    parsed = urlparse(url)
    if parsed.scheme != "http" or parsed.hostname not in {"127.0.0.1", "localhost", "::1"}:
        raise ValueError("Novahn stdio proxy only connects to local HTTP endpoints")
    token = os.environ.get("NOVAHN_PROXY_TOKEN", "")
    headers = {"Authorization": f"Bearer {token}"} if token else {}
    client = Client(StreamableHttpTransport(url, headers=headers), timeout=90)
    # FastMCP owns stdout for MCP protocol frames; logs remain on stderr.
    create_proxy(client, name="Novahn Studio").run(transport="stdio", show_banner=False)


if __name__ == "__main__":
    main()
