
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
                document.getElementById('connection_issue_icon1').style.display = "block";
                document.getElementById('connection_issue_icon2').style.display = "none";
                document.getElementById('connection_issue_icon3').style.display = "none";
                document.getElementById('connection_issue_icon4').style.display = "none";
            }
            else if (data.OutputCongestion > 0.50) {
                document.getElementById('connection_issue_icon1').style.display = "none";
                document.getElementById('connection_issue_icon2').style.display = "block";
                document.getElementById('connection_issue_icon3').style.display = "none";
                document.getElementById('connection_issue_icon4').style.display = "none";
            }
            else if (data.OutputCongestion > 0.25) {
                document.getElementById('connection_issue_icon1').style.display = "none";
                document.getElementById('connection_issue_icon2').style.display = "none";
                document.getElementById('connection_issue_icon3').style.display = "block";
                document.getElementById('connection_issue_icon4').style.display = "none";
            }
            else {
                document.getElementById('connection_issue_icon1').style.display = "none";
                document.getElementById('connection_issue_icon2').style.display = "none";
                document.getElementById('connection_issue_icon3').style.display = "none";
                document.getElementById('connection_issue_icon4').style.display = "block";
            }
            break;
        case 'SkippedFrames':
            textReference.textContent = 'Skipping frames!';
            document.getElementById('connection_issue_icon1').style.display = "none";
            document.getElementById('connection_issue_icon2').style.display = "none";
            document.getElementById('connection_issue_icon3').style.display = "none";
            document.getElementById('connection_issue_icon4').style.display = "none";
            break;
    }
}

function HideBar() {
    if (state !== "Hidden") {
        uploadStatContainer.classList.add('upload-stat-fade-out');
        document.getElementById('connection_issue_icon1').style.display = "none";
        document.getElementById('connection_issue_icon2').style.display = "none";
        document.getElementById('connection_issue_icon3').style.display = "none";
        document.getElementById('connection_issue_icon4').style.display = "none";
        state = "Hidden";
    }
}

const debug = true;

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
