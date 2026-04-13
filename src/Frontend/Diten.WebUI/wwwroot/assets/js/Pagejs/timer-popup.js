const params = new URLSearchParams(window.location.search);
const taskId = params.get("taskId");
const start = new Date(params.get("start"));

document.getElementById("taskName").textContent =
    "Tracking Task " + taskId;

let interval = setInterval(() => {
    const diff = Math.floor((Date.now() - start.getTime()) / 1000);
    const mm = String(Math.floor(diff / 60)).padStart(2, "0");
    const ss = String(diff % 60).padStart(2, "0");
    document.getElementById("timer").textContent = `${mm}:${ss}`;
}, 1000);

// STOP API
document.getElementById("stopBtn").onclick = async () => {
    await fetch(`/services/DitenPPM/TimeTracker/Stop`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ taskId })
    });

    clearInterval(interval);
    window.close();   // 🔥 Pencere kendini kapatır
};
