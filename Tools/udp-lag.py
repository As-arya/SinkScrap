"""Loopback-only UDP test proxy: 75 ms each way and 2% packet loss."""
import argparse
import asyncio
import json
import random
from pathlib import Path


class Proxy:
    def __init__(self, delay, loss, report):
        self.delay, self.loss, self.report = delay, loss, Path(report)
        self.random = random.Random(20261013)
        self.stats = {"delay_each_way_ms": round(delay * 1000), "configured_loss": loss, "received": 0, "dropped": 0, "scheduled": 0}
        self.peers = {}

    def forward(self, transport, packet, destination=None):
        self.stats["received"] += 1
        if self.random.random() < self.loss:
            self.stats["dropped"] += 1
            return
        self.stats["scheduled"] += 1
        asyncio.get_running_loop().call_later(self.delay, lambda: None if transport.is_closing() else transport.sendto(packet, destination))

    async def peer(self, downstream, address, packet):
        key = (downstream, address)
        if key not in self.peers:
            self.peers[key] = asyncio.create_task(asyncio.get_running_loop().create_datagram_endpoint(lambda: Upstream(self, downstream, address), remote_addr=("127.0.0.1", 7777)))
        transport, _ = await self.peers[key]
        self.forward(transport, packet)

    def write(self):
        self.report.parent.mkdir(parents=True, exist_ok=True)
        self.report.write_text(json.dumps(self.stats, indent=2), encoding="utf-8")


class Downstream(asyncio.DatagramProtocol):
    def __init__(self, proxy): self.proxy = proxy
    def connection_made(self, transport): self.transport = transport
    def datagram_received(self, data, addr): asyncio.create_task(self.proxy.peer(self.transport, addr, data))
    def error_received(self, exc): pass


class Upstream(asyncio.DatagramProtocol):
    def __init__(self, proxy, transport, address): self.proxy, self.downstream, self.address = proxy, transport, address
    def datagram_received(self, data, addr): self.proxy.forward(self.downstream, data, self.address)
    def error_received(self, exc): pass


async def main(args):
    proxy = Proxy(args.delay_ms / 1000, args.loss, args.report)
    listeners = []
    for port in (7778, 7779, 7780):
        transport, _ = await asyncio.get_running_loop().create_datagram_endpoint(lambda: Downstream(proxy), local_addr=("127.0.0.1", port))
        listeners.append(transport)
    print("UDP QA proxy listening on loopback ports 7778-7780", flush=True)
    try:
        for _ in range(args.duration // 2):
            await asyncio.sleep(2)
            proxy.write()
    finally:
        proxy.write()
        for transport in listeners: transport.close()
        for future in proxy.peers.values():
            if future.done() and not future.cancelled(): future.result()[0].close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--delay-ms", type=int, default=75)
    parser.add_argument("--loss", type=float, default=0.02)
    parser.add_argument("--duration", type=int, default=600)
    parser.add_argument("--report", required=True)
    asyncio.run(main(parser.parse_args()))
