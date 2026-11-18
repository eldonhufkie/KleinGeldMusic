// Sends the file chosen in a file input straight to blob storage through an upload link. XMLHttpRequest, because
// fetch can't report upload progress. Resolves to whether storage accepted the file.
export function upload(input, url, progress, index = 0) {
    const file = input.files[index];
    return new Promise(resolve => {
        const request = new XMLHttpRequest();
        request.open("PUT", url);
        request.setRequestHeader("x-ms-blob-type", "BlockBlob");
        request.setRequestHeader("Content-Type", file.type || "application/octet-stream");
        request.upload.onprogress = event => {
            if (event.lengthComputable) {
                progress.invokeMethodAsync("Report", Math.round(100 * event.loaded / event.total));
            }
        };
        request.onload = () => resolve(request.status >= 200 && request.status < 300);
        request.onerror = () => resolve(false);
        request.send(file);
    });
}

export function chosenFiles(input) {
    return Array.from(input.files, file => ({ name: file.name, size: file.size }));
}

export function clear(input) {
    input.value = "";
}
