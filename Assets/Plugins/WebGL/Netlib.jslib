// Bridge to the Poki Netlib (peer-to-peer WebRTC). The library and our wrapper (BTGNet) are in BtgNet.jspre. Nothing here calls back into C#:
// the wrapper queues the events and C# polls them once a frame (NetlibTransport.cs).
mergeInto(LibraryManager.library, {
    Netlib_Init: function (gameIdPtr) {
        if (!window.btgNet) window.btgNet = new BTGNet();
        window.btgNet.init(UTF8ToString(gameIdPtr));
    },
    Netlib_List: function () { if (window.btgNet) window.btgNet.list(); },
    Netlib_Create: function (customPtr) { if (window.btgNet) window.btgNet.create(UTF8ToString(customPtr)); },
    Netlib_Join: function (codePtr) { if (window.btgNet) window.btgNet.join(UTF8ToString(codePtr)); },
    Netlib_Send: function (channelPtr, textPtr) { if (window.btgNet) window.btgNet.send(UTF8ToString(channelPtr), UTF8ToString(textPtr)); },
    Netlib_Leave: function () { if (window.btgNet) window.btgNet.leave(); },
    Netlib_Close: function () { if (window.btgNet) window.btgNet.close(); },
    // The next queued event as a JSON string (to be freed with Netlib_Free), or 0 when there is none.
    Netlib_Poll: function () {
        var s = window.btgNet ? window.btgNet.poll() : null;
        if (!s) return 0;
        var size = lengthBytesUTF8(s) + 1;
        var buffer = _malloc(size);
        stringToUTF8(s, buffer, size);
        return buffer;
    },
    Netlib_Free: function (ptr) { _free(ptr); }
});
