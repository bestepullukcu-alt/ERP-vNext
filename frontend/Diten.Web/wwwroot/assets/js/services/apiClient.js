/**
 * Diten API Client
 * Reusable fetch wrapper for backend communication
 */

const API_CONFIG = {
    // Falls back to a default if window.APP_CONFIG is not defined (e.g. in tests)
    BASE_URL: window.APP_CONFIG?.API_BASE_URL || 'https://localhost:5004',
};

class ApiClient {
    async get(endpoint) {
        return this._request(endpoint, 'GET');
    }

    async post(endpoint, data) {
        return this._request(endpoint, 'POST', data);
    }

    async put(endpoint, data) {
        return this._request(endpoint, 'PUT', data);
    }

    async delete(endpoint) {
        return this._request(endpoint, 'DELETE');
    }

    async _request(endpoint, method, data = null) {
        const url = `${API_CONFIG.BASE_URL}/${endpoint}`;
        const options = {
            method,
            headers: {
                'Content-Type': 'application/json',
            }
        };

        if (data) {
            options.body = JSON.stringify(data);
        }

        try {
            const response = await fetch(url, options);
            const result = await response.json();

            if (!response.ok) {
                console.error(`API Error [${response.status}]:`, result);
                throw new Error(result.message || 'An error occurred during the request.');
            }

            return result;
        } catch (error) {
            console.error('Fetch Error:', error);
            throw error;
        }
    }
}

const apiClient = new ApiClient();
export default apiClient;
