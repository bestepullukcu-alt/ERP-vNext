'use strict';

/**
 * MOD-0150 Contact location picker — inline Leaflet + OpenStreetMap, mirroring the MOD-0149 Account picker.
 * Click/drag the marker or search a place to reverse-geocode into the Address field and auto-select the
 * Country/City/District dropdowns. A Contact is a person (not a physical workplace), so NO Latitude/Longitude is
 * stored — the map only assists filling the location fields; all fields remain manually editable. Tiles/geocoding
 * are free OSM services (no API key). Same set/selection logic (kebab-normalized match) as the Account picker.
 */
(function () {
    const mapEl = document.getElementById('contactMap');
    if (!mapEl || typeof L === 'undefined') {
        return;
    }

    // Vendored marker icons live next to leaflet.css; point Leaflet at them so the default marker renders.
    const imgBase = '/assets/vendor/libs/leaflet/images/';
    L.Icon.Default.mergeOptions({
        iconRetinaUrl: imgBase + 'marker-icon-2x.png',
        iconUrl: imgBase + 'marker-icon.png',
        shadowUrl: imgBase + 'marker-shadow.png'
    });

    const addressInput = document.getElementById('AddressLine');
    const searchInput = document.getElementById('contactMapSearch');
    const searchBtn = document.getElementById('btnContactMapSearch');
    const myLocationBtn = document.getElementById('btnContactUseMyLocation');

    // Default view: Turkey centre (Contact stores no coordinates, so there is no saved point to restore).
    const DEFAULT_CENTER = [39.0, 35.0];
    const DEFAULT_ZOOM = 5;
    const POINT_ZOOM = 15;

    const map = L.map(mapEl).setView(DEFAULT_CENTER, DEFAULT_ZOOM);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    // Leaflet needs a size recalculation when it initialises inside a card that was laid out before the map existed.
    setTimeout(() => map.invalidateSize(), 200);

    let marker = null;
    const setMarker = (lat, lng) => {
        if (marker) {
            marker.setLatLng([lat, lng]);
        } else {
            marker = L.marker([lat, lng], { draggable: true }).addTo(map);
            marker.on('dragend', () => {
                const p = marker.getLatLng();
                reverseGeocode(p.lat, p.lng);
            });
        }
    };

    const pick = (lat, lng, { fly = true, geocode = true } = {}) => {
        setMarker(lat, lng);
        if (fly) map.setView([lat, lng], Math.max(map.getZoom(), POINT_ZOOM));
        if (geocode) reverseGeocode(lat, lng);
    };

    map.on('click', (e) => pick(e.latlng.lat, e.latlng.lng));

    // Normalize a place name to the same kebab-ascii code used to author the reference values (İ→i, ş→s, ğ→g, …),
    // so a geocoded name ("Edirne", "Keşan") can be matched to a dropdown option value ("edirne", "kesan").
    const kebab = (s) => (s || '')
        .replace(/İ/g, 'I').replace(/ı/g, 'i').replace(/ş/g, 's').replace(/Ş/g, 'S')
        .replace(/ğ/g, 'g').replace(/Ğ/g, 'G').replace(/ç/g, 'c').replace(/Ç/g, 'C')
        .replace(/ö/g, 'o').replace(/Ö/g, 'O').replace(/ü/g, 'u').replace(/Ü/g, 'U')
        .normalize('NFKD').replace(/[̀-ͯ]/g, '')
        .toLowerCase().trim().replace(/\s+/g, '-');

    // Select the dropdown option that matches the geocoded location, by exact code (e.g. country_code "tr"),
    // then by kebab(name)==value, kebab(optionText), or a case-insensitive text match. No match → left unchanged
    // (e.g. when the reference set is not published yet, so the dropdown is empty). Sync select2 via jQuery change.
    const selectOption = (selectId, candidates, exactCode) => {
        const sel = document.getElementById(selectId);
        if (!sel) return;
        const opts = Array.from(sel.options).filter(o => o.value);
        let match = null;
        if (exactCode) {
            match = opts.find(o => o.value.toLowerCase() === String(exactCode).toLowerCase());
        }
        if (!match) {
            for (const name of candidates) {
                if (!name) continue;
                const k = kebab(name);
                match = opts.find(o => o.value.toLowerCase() === k)
                    || opts.find(o => kebab(o.textContent) === k)
                    || opts.find(o => o.textContent.trim().toLowerCase() === String(name).trim().toLowerCase());
                if (match) break;
            }
        }
        if (match && sel.value !== match.value) {
            sel.value = match.value;
            if (window.jQuery) window.jQuery(sel).trigger('change');
        }
    };

    // Map Nominatim's structured `address` object onto the independent Country/City/District dropdowns. Turkey's
    // province can arrive as province/state; the district as county/town/city_district/district/municipality.
    const applyAddressParts = (addr) => {
        if (!addr) return;
        selectOption('CountryRef', [addr.country], addr.country_code);
        selectOption('CityRef', [addr.province, addr.state, addr.city], null);
        selectOption('DistrictRef', [addr.county, addr.town, addr.city_district, addr.district, addr.municipality], null);
    };

    // --- Nominatim (OSM) geocoding. Free, ~1 req/sec; the browser sends Referer automatically. ---
    const reverseGeocode = async (lat, lng) => {
        try {
            const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&addressdetails=1&lat=${lat}&lon=${lng}&accept-language=tr`;
            const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) return;
            const data = await res.json();
            if (data && data.display_name && addressInput) {
                addressInput.value = data.display_name.slice(0, 256);
            }
            applyAddressParts(data && data.address);
        } catch (err) {
            console.debug('[ContactMap] reverse geocode failed', err);
        }
    };

    const forwardGeocode = async (query) => {
        if (!query) return;
        try {
            const url = `https://nominatim.openstreetmap.org/search?format=jsonv2&q=${encodeURIComponent(query)}&limit=1&accept-language=tr`;
            const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) return;
            const data = await res.json();
            if (Array.isArray(data) && data.length > 0) {
                pick(parseFloat(data[0].lat), parseFloat(data[0].lon));
            } else {
                window.showToast?.(window.L10n?.NoResults || 'No results.', 'warning');
            }
        } catch (err) {
            console.debug('[ContactMap] forward geocode failed', err);
        }
    };

    searchBtn?.addEventListener('click', () => forwardGeocode(searchInput?.value?.trim()));
    searchInput?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            forwardGeocode(searchInput.value.trim());
        }
    });

    myLocationBtn?.addEventListener('click', () => {
        if (!navigator.geolocation) {
            window.showToast?.(window.L10n?.GeolocationUnavailable || 'Geolocation is not available.', 'warning');
            return;
        }
        navigator.geolocation.getCurrentPosition(
            (pos) => pick(pos.coords.latitude, pos.coords.longitude),
            () => window.showToast?.(window.L10n?.GeolocationDenied || 'Location permission denied.', 'warning'));
    });
})();
