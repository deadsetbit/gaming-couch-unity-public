var LibraryGCServer = {
  $state: {},

  _GCGameMessageToJS: function (
    bufferPtr,
    offset,
    count,
    gcClientId,
    isReliable
  ) {
    window.gcHandleGameMessageFromUnity(
      HEAPU8.buffer.slice(bufferPtr + offset, bufferPtr + count - offset),
      Number(gcClientId),
      Boolean(isReliable)
    );
  },
};

autoAddDeps(LibraryGCServer, "$state");
mergeInto(LibraryManager.library, LibraryGCServer);
