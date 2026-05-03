
import { sbClient } from './ws-client.js';

var textReference = null;
var sliderReference = null;
var uploadStatContainer = null;
var connectionBarIcon = null;
var state = "Hidden";

function SetReferences() {
    if (textReference === null) {
        textReference = document.getElementById('upload-stat-time');
    }

    if (sliderReference === null) {
        sliderReference = document.getElementById('upload-stat-fill');
    }

    if (uploadStatContainer === null) {
        uploadStatContainer = document.getElementById('upload-stat-container');
    }

    if (uploadStatContainer === null) {
        uploadStatContainer = document.getElementById('upload-stat-container');
    }

    if (connectionBarIcon === null) {
        connectionBarIcon = document.querySelector('.upload-bars-image svg');
    }
}

function SetStyle(style) {
    if (state === "Hidden" && style !== "Hidden") {
        uploadStatContainer.classList.remove('upload-stat-fade-out');
    }

    state = style;
    switch (style) {
        case 'Reconnecting':
            textReference.textContent = "Reconnecting...";
            break;
        case 'DroppedFrames':
            textReference.textContent = "";
            break;
        case 'SkippedFrames':
            textReference.textContent = "";
            break;
    }


}

function SetStats(data) {
    switch (state) {
        case 'DroppedFrames':
            textReference.textContent = 'Dropping frames!';
            const iconContainer = document.querySelector('.upload-bars-image');
            if (data.OutputCongestion > 0.9) {
                connectionBarIcon.style.clipPath = 'inset(0 14px 0 0)';
                iconContainer.style.color = 'rgb(255, 0, 0)';
            }
            else if (data.OutputCongestion > 0.50) {
                connectionBarIcon.style.clipPath = 'inset(0 9px 0 0)';
                iconContainer.style.color = 'rgb(255, 64, 0)';
            }
            else if (data.OutputCongestion > 0.25) {
                connectionBarIcon.style.clipPath = 'inset(0 5px 0 0)';
                iconContainer.style.color = 'rgb(255, 128, 0)';
            }
            else {
                connectionBarIcon.style.clipPath = 'none';
                iconContainer.style.color = 'rgb(255, 255, 0)';
            }
            break;
        case 'SkippedFrames':
            textReference.textContent = 'Skipping frames!';
            break;
    }
}

function HideBar() {
    if (state !== "Hidden") {
        uploadStatContainer.classList.add('upload-stat-fade-out');
        connectionBarIcon.style.display = 'none';
        state = "Hidden";
    }
}

const debug = false;

sbClient.on('General.Custom', (data) => {
    if (data === null || data.data === null) return;
    if (data.data.Type.startsWith("StreamStatusUpdate_") === false) return;

    if (debug) {
        console.log("Setting stats to: " + JSON.stringify(data.data));
    }

    SetReferences();
    switch (data.data.Type) {
        case "StreamStatusUpdate_SetStyle":
            SetStyle(data.data.Style);
            break;
        case "StreamStatusUpdate_SetStats":
            SetStats(data.data);
            break;
        case "StreamStatusUpdate_Hide":
            HideBar();
            break;
        default:
            return;
    }
});
