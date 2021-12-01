A test game for experimenting with physics-network interactions.

Basic idea is pure oldschool server-side authority -- clients send inputs, server sends back game state.
For physically simulated games, 100-200ms latency seems to actually work Just Fine in terms of input delay. It's noticable in a real sense, but easy to adapt to.
Wouldn't work for hitscan-shooting physics games, but works well enough otherwise.
I'm pretty sure most TV-Console or phone setups actually have similar or longer input latency anyways. >.>

Some latency mitigation techniques used -- camera responds instantly to player input so player gets immediate feedback on inputs (even if they don't actually do anything yet).
Server sends partial state (experimenting with under-MTU only... not sure ideal), rest is simulated client-side. Leads to some desyncs, still working on it. Focus on other player-inputs and recently interacted with, nearby, or high-velocity objects for updates.

Mostly made when fall guys got big because the networking was just painfully bad.

cyFight is the client, cyServer is the server, cySim is the middle-ground simulation that both use.
