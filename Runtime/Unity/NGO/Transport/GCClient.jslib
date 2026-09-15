var LibraryGCClient = {
  $state: {},

  _GCGameMessageToJS: function (bufferPtr, offset, count, isReliable) {
    window.gcNetHandleJsonMessage(
      HEAPU8.buffer.slice(bufferPtr + offset, bufferPtr + count - offset),
      Boolean(isReliable)
    );
  },
};

autoAddDeps(LibraryGCClient, "$state");
mergeInto(LibraryManager.library, LibraryGCClient);
