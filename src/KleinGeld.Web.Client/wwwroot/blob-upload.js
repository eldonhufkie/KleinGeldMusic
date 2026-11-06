// Uploads a chosen file straight to blob storage through a SAS link. XMLHttpRequest, because fetch can't report
// upload progress, and the file stays in the browser rather than passing through WebAssembly memory.

export function chosenFiles(input) {
    return Array.from(input.files, file => ({ name: file.name, size: file.size }));
}

export function clear(input) {
    input.value = "";
}

/** Resolves to true once the blob exists. Calls progress.invokeMethodAsync("OnProgress", percent) along the way. */
export function upload(input, index, link, progress) {
    const file = input.files[index];
    return new Promise(resolve => {
        const request = new XMLHttpRequest();
        request.open("PUT", link);
        request.setRequestHeader("x-ms-blob-type", "BlockBlob");
        request.upload.onprogress = e => {
            if (e.lengthComputable) {
                progress.invokeMethodAsync("OnProgress", Math.floor(e.loaded * 100 / e.total));
            }
        };
        request.onload = () => resolve(request.status === 201);
        request.onerror = () => resolve(false);
        request.send(file);
    });
}
