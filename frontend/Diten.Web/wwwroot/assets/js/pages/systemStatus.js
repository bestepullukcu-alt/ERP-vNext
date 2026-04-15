import apiClient from '../services/apiClient.js';

document.addEventListener('DOMContentLoaded', async () => {
    const statusDot = document.getElementById('status-dot');
    const statusText = document.getElementById('status-text');
    const pingBtn = document.getElementById('ping-btn');

    if (pingBtn) {
        pingBtn.addEventListener('click', async () => {
            try {
                statusText.innerText = 'Checking...';
                const response = await apiClient.get('api/v1/system/ping');

                if (response.success) {
                    statusDot.className = 'status-dot online';
                    statusText.innerText = response.data;
                } else {
                    statusDot.className = 'status-dot offline';
                    statusText.innerText = 'Error: ' + response.message;
                }
            } catch (error) {
                statusDot.className = 'status-dot offline';
                statusText.innerText = 'Backend connection failed.';
                console.error('System Check Error:', error);
            }
        });
    }
});
