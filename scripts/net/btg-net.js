// Thin bridge between the Poki Netlib (window.netlib, the "legacy" browser build) and Unity's C# code.
// Unity cannot be called back easily from here, so everything is a queue: Netlib's events are pushed as JSON strings and C# polls them once a frame
// (see Assets/Plugins/WebGL/Netlib.jslib and Assets/Scripts/Net/NetlibTransport.cs).
//   { t: "ready" }                      the signaling connection is up: create / list / join can be used now
//   { t: "lobby", d: "<code>" }         a lobby was created or joined
//   { t: "list", d: "<json>" }          the answer to list(): an array of { code, playerCount, maxPlayers, customData ... }
//   { t: "connected" | "disconnected", d: "<peer id>" }
//   { t: "message", c: "<channel>", d: "<text>", p: "<peer id>" }
//   { t: "left" | "failed" | "closed" }
//   { t: "error", d: "<text>" }
(function (g) {
    function Client() { this.q = []; this.net = null; }

    Client.prototype.push = function (o) {
        if (this.q.length > 800) this.q.shift(); // nobody is polling: do not grow for ever
        this.q.push(JSON.stringify(o));
    };

    Client.prototype.init = function (gameId) {
        var self = this;
        if (this.net) this.close();
        if (!g.netlib || !g.netlib.Network) { this.push({ t: "error", d: "netlib missing" }); return; }
        var n;
        try { n = new g.netlib.Network(gameId); } catch (e) { this.push({ t: "error", d: "init: " + e }); return; }
        this.net = n;
        n.on("ready", function () { self.push({ t: "ready" }); });
        n.on("lobby", function (code) { self.push({ t: "lobby", d: code }); });
        n.on("connected", function (p) { self.push({ t: "connected", d: p.id }); });
        n.on("disconnected", function (p) { self.push({ t: "disconnected", d: p.id }); });
        n.on("message", function (p, ch, data) { self.push({ t: "message", c: ch, d: typeof data === "string" ? data : "", p: p.id }); });
        n.on("left", function () { self.push({ t: "left" }); });
        n.on("failed", function () { self.push({ t: "failed" }); });
        n.on("close", function () { self.push({ t: "closed" }); });
        n.on("signalingerror", function (e) { self.push({ t: "error", d: "signaling: " + (e && e.message ? e.message : e) }); });
        n.on("rtcerror", function () { self.push({ t: "error", d: "rtc" }); });
    };

    Client.prototype.list = function () {
        var self = this;
        if (!this.net) return;
        this.net.list({}, {}, 30).then(function (lobbies) { self.push({ t: "list", d: JSON.stringify(lobbies || []) }); })
            .catch(function (e) { self.push({ t: "error", d: "list: " + e }); });
    };

    Client.prototype.create = function (customJson) {
        var self = this;
        if (!this.net) return;
        var custom = {};
        try { custom = JSON.parse(customJson || "{}"); } catch (e) { }
        this.net.create({ public: true, maxPlayers: 2, customData: custom }).catch(function (e) { self.push({ t: "error", d: "create: " + e }); });
    };

    Client.prototype.join = function (code) {
        var self = this;
        if (!this.net) return;
        this.net.join(code).catch(function (e) { self.push({ t: "error", d: "join: " + e }); });
    };

    Client.prototype.send = function (channel, text) {
        if (!this.net) return;
        try { this.net.broadcast(channel, text); } catch (e) { }
    };

    Client.prototype.leave = function () {
        if (this.net) { try { this.net.leave(); } catch (e) { } }
    };

    Client.prototype.close = function () {
        if (this.net) { try { this.net.close(); } catch (e) { } this.net = null; }
    };

    Client.prototype.poll = function () { return this.q.length ? this.q.shift() : null; };

    g.BTGNet = Client;
})(typeof globalThis !== "undefined" ? globalThis : window);
