// Frontend logic for artist analysis UI
document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('searchForm');
    const input = document.getElementById('artistInput');
    const status = document.getElementById('status');
    const resultArea = document.getElementById('result');

    function setStatus(text, isError = false) {
        status.textContent = text;
        status.style.color = isError ? '#f87171' : '';
    }

    // Render timeline JSON (string) into HTML list. Accepts empty string.
    function renderTimeline(timelineStr) {
        if (!timelineStr) return '<div class="no-timeline">No timeline available</div>';

        // Try parse. Timeline may be a JSON array or plain text.
        try {
            const parsed = JSON.parse(timelineStr);
            if (Array.isArray(parsed)) {
                return '<ul class="timeline-list">' + parsed.map((item, idx) => {
                    const year = item.year ? escapeHtml(item.year) : '';
                    const event = item.event ? escapeHtml(item.event) : (item.title ? escapeHtml(item.title) : '');
                    const detailsRaw = item.details ? String(item.details) : '';
                    const details = escapeHtml(detailsRaw);
                    const short = details.length > 220 ? escapeHtml(detailsRaw.substring(0, 220)) + '...' : details;
                    const needsToggle = details.length > 220;
                    return `
                      <li class="timeline-item">
                        <div class="t-year">${year}</div>
                        <div class="t-content">
                          <strong>${event}</strong>
                          <div class="t-details" data-idx="${idx}">
                            <span class="short">${short}</span>
                            ${needsToggle ? `<span class="full" style="display:none">${details}</span>` : ''}
                            ${needsToggle ? `<button class="t-toggle" data-idx="${idx}">Mostrar más</button>` : ''}
                          </div>
                        </div>
                      </li>`;
                }).join('') + '</ul>';
            }
            // if parsed is an object with text
            return `<div class="timeline-text">${escapeHtml(JSON.stringify(parsed))}</div>`;
        } catch (e) {
            // Not JSON, show as preformatted text
            return `<pre class="timeline-pre">${escapeHtml(timelineStr)}</pre>`;
        }
    }

    function renderCareerRisk(careerRiskStr) {
        if (!careerRiskStr) return '<div class="no-career-risk">No data</div>';

        try {
            const obj = typeof careerRiskStr === 'string' ? JSON.parse(careerRiskStr) : careerRiskStr;

            const level = escapeHtml(obj.risk_level || obj.riskLevel || 'Unknown');
            const score = typeof obj.score === 'number' ? obj.score : parseFloat(obj.score || 0);
            const explanation = escapeHtml(obj.explanation || '');
            const recs = (obj.recommendations || obj.recs || []).map(r => `<li>${escapeHtml(r)}</li>`).join('');

            return `
                <div class="career-risk-card">
                    <div class="cr-header">
                        <div class="cr-level">Nivel: ${level}</div>
                        <div class="cr-score">Score riesgo: ${score.toFixed(2)}</div>
                    </div>
                    <div class="cr-expl">${explanation}</div>
                    <ul class="cr-recs">${recs}</ul>
                </div>
            `;
        } catch (e) {
            return `<pre class="career-risk-pre">${escapeHtml(careerRiskStr)}</pre>`;
        }
    }

    function renderResult(data) {
        if (!data) {
            resultArea.innerHTML = '';
            return;
        }

        const imageHtml = data.imageUrl
            ? `<img class="artist-thumb" src="${escapeHtml(data.imageUrl)}" alt="${escapeHtml(data.artistName)}"/>`
            : `<div class="artist-thumb placeholder">No image</div>`;

        const timelineHtml = renderTimeline(data.timeline);
        const careerRiskHtml = renderCareerRisk(data.careerRisk);

        const html = `
      <div class="result-card">
        <div class="result-header">
          <div class="header-left">
            ${imageHtml}
            <div class="title-block">
              <h2>${escapeHtml(data.artistName)}</h2>

              <!-- CLASIFICACIÓN ESTILIZADA -->
              <div class="classification-badge ${getClassificationClass(data.classification)}">
                ${escapeHtml(data.classification || 'Sin clasificación')}
              </div>

              <!-- ESTADO DEL PERFIL -->
              <div class="profile-status ${getProfileStatusClass(data.score)}">
                ${getProfileStatusText(data.score)}
              </div>

            </div>
          </div>
        </div>

        <div class="columns">
          <div class="col">
            <h3>Biografía</h3>
            <div class="bio" id="bioFull">${escapeHtml(data.biography)}</div>
            <button class="btn small toggle-btn" id="toggleBio">Mostrar completa</button>
          </div>

          <div class="col">
            <h3>Línea de Tiempo</h3>
            <div class="timeline" id="timeline">${timelineHtml}</div>
          </div>
        </div>
      </div>
    `;

        resultArea.innerHTML = html;
        // If server didn't provide a timeline, generate a simple client-side fallback
        if (!data.timeline || data.timeline.trim() === '') {
            try {
                const timelineDiv = document.getElementById('timeline');
                if (timelineDiv) timelineDiv.innerHTML = renderTimeline(generateClientTimelineJson(data.biography));
            } catch (e) {
                console.error('client timeline fallback failed', e);
            }
        }


        const bioEl = document.getElementById('bioFull');
        const toggle = document.getElementById('toggleBio');

        let expanded = false;
        toggle.addEventListener('click', () => {
            expanded = !expanded;
            bioEl.style.maxHeight = expanded ? '800px' : '120px';
            toggle.textContent = expanded ? 'Ocultar completa' : 'Mostrar completa';
        });

        // timeline toggle buttons (for details)
        document.querySelectorAll('.t-toggle').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const idx = btn.getAttribute('data-idx');
                const container = document.querySelector(`.t-details[data-idx="${idx}"]`);
                if (!container) return;
                const shortEl = container.querySelector('.short');
                const fullEl = container.querySelector('.full');
                if (!fullEl) return;
                const isHidden = fullEl.style.display === 'none';
                fullEl.style.display = isHidden ? 'inline' : 'none';
                shortEl.style.display = isHidden ? 'none' : 'inline';
                btn.textContent = isHidden ? 'Mostrar menos' : 'Mostrar más';
            });
        });
    }

    // ===== ESTADO PERFIL =====

    function getProfileStatusText(score) {
        if (score >= 0.9) return "Perfil completo";
        if (score >= 0.6) return "Perfil detallado";
        if (score >= 0.3) return "Perfil básico";
        return "Información limitada";
    }

    function getProfileStatusClass(score) {
        if (score >= 0.9) return "status-complete";
        if (score >= 0.6) return "status-detailed";
        if (score >= 0.3) return "status-basic";
        return "status-limited";
    }

    // ===== CLASIFICACIÓN =====

    function getClassificationClass(classification) {
        if (!classification) return "class-unknown";

        const text = classification.toLowerCase();

        if (text.includes("exitosa")) return "class-success";
        if (text.includes("emergente")) return "class-rising";
        if (text.includes("leyenda")) return "class-legend";
        if (text.includes("independiente")) return "class-indie";

        return "class-default";
    }

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const name = input.value.trim();
        if (!name) {
            setStatus('Ingresa el nombre de un artista', true);
            return;
        }

        setStatus('Analizando...');
        resultArea.innerHTML = '';

        try {
            const resp = await fetch(`/api/artist/analyze?name=${encodeURIComponent(name)}`);
            if (!resp.ok) {
                const text = await resp.text();
                throw new Error(text || `Error ${resp.status}`);
            }

            const data = await resp.json();
            renderResult(data);
            setStatus('Análisis completado');
        } catch (err) {
            console.error(err);
            setStatus(err.message || 'Ocurrió un error', true);
        }
    });

    // Announcement close button
    const announceClose = document.getElementById('announceClose');
    const announcement = document.getElementById('announcement');
    if (announceClose && announcement) {
        announceClose.addEventListener('click', () => { announcement.style.display = 'none'; });
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/\"/g, '&quot;')
            .replace(/'/g, '&#039;')
            .replace(/\n/g, '<br/>');
    }

    // Client-side simple timeline generator: extract sentences with years or notable phrases
    function generateClientTimelineJson(biography) {
        if (!biography) return '[]';

        const sentences = biography.split(/[\.\n]+/).map(s => s.trim()).filter(s => s.length > 20);
        const yearRegex = /(19|20)\d{2}/;
        const items = [];

        for (let i = 0; i < sentences.length && items.length < 6; i++) {
            const s = sentences[i];
            let year = null;
            const m = s.match(yearRegex);
            if (m) year = m[0];
            items.push({ year: year, event: s.length > 60 ? s.substring(0, 60) + '...' : s, details: s });
        }

        return JSON.stringify(items);
    }
});