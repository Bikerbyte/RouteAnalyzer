const form = document.querySelector("#diagnostic-form");

if (form) {
  const states = {
    empty: document.querySelector("#empty-state"),
    loading: document.querySelector("#loading-state"),
    error: document.querySelector("#error-state"),
    result: document.querySelector("#result-state")
  };

  const showState = (name) => {
    Object.entries(states).forEach(([key, element]) => {
      element?.classList.toggle("hidden", key !== name);
    });
  };

  const targetInput = document.querySelector("#targetHost");
  document.querySelectorAll("[data-preset]").forEach((button) => {
    button.addEventListener("click", () => {
      targetInput.value = button.dataset.preset;
      targetInput.focus();
    });
  });

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    showState("loading");

    const submitButton = form.querySelector(".run-button");
    submitButton.disabled = true;

    try {
      const response = await fetch("/api/diagnostics/run", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          targetHost: targetInput.value,
          language: "zh-TW",
          pingCount: Number(document.querySelector("#pingCount").value),
          maxHops: Number(document.querySelector("#maxHops").value),
          includeDnsCheck: document.querySelector("#includeDnsCheck").checked,
          includeHttpsCheck: document.querySelector("#includeHttpsCheck").checked,
          includeGeoDetails: document.querySelector("#includeGeoDetails").checked
        })
      });

      const payload = await response.json();
      if (!response.ok) {
        throw new Error(payload.message || "Diagnostic failed.");
      }

      renderResult(payload);
      showState("result");
    } catch (error) {
      states.error.textContent = error.message;
      showState("error");
    } finally {
      submitButton.disabled = false;
    }
  });

  document.querySelector("#copy-button")?.addEventListener("click", async () => {
    const copyText = document.querySelector("#copy-text").value;
    await navigator.clipboard.writeText(copyText);
    const button = document.querySelector("#copy-button");
    const original = button.textContent;
    button.textContent = "Copied";
    setTimeout(() => {
      button.textContent = original;
    }, 1200);
  });
}

function renderResult(payload) {
  const summary = payload.summary;
  const tone = "good";

  document.querySelector("#result-title").textContent = "Connection snapshot";
  document.querySelector("#result-summary").textContent = summary.overview;
  document.querySelector("#overview-text").textContent = summary.overview;

  const status = document.querySelector("#result-status");
  status.textContent = summary.captureStatus;
  status.className = `status-pill ${tone}`;

  renderMetric("#metric-latency", summary.latency);
  renderMetric("#metric-loss", summary.packetLoss);
  renderMetric("#metric-dns", summary.dns);
  renderMetric("#metric-tcp", summary.tcp);
  renderList("#signal-list", summary.signals);
  renderHopChart(summary.hops);

  document.querySelector("#copy-text").value = summary.copyText;
  document.querySelector("#report-link").href = payload.reportUrl;
}

function renderMetric(selector, metric) {
  const element = document.querySelector(selector);
  element.className = `metric ${metric.tone}`;
  element.innerHTML = `<span>${escapeHtml(metric.label)}</span><strong>${escapeHtml(metric.value)}</strong>`;
}

function renderList(selector, items) {
  const element = document.querySelector(selector);
  element.innerHTML = "";

  if (!items.length) {
    const item = document.createElement("li");
    item.textContent = "No notable signals.";
    element.appendChild(item);
    return;
  }

  items.forEach((text) => {
    const item = document.createElement("li");
    item.textContent = text;
    element.appendChild(item);
  });
}

function renderHopChart(hops) {
  const chart = document.querySelector("#hop-chart");
  chart.innerHTML = "";
  document.querySelector("#hop-count").textContent = `${hops.length} hops`;

  const maxLatency = Math.max(...hops.map((hop) => hop.averageLatencyMs || 0), 20);
  const visibleHops = hops.slice(0, 36);

  visibleHops.forEach((hop) => {
    const bar = document.createElement("div");
    const latency = hop.averageLatencyMs || 0;
    const height = hop.isTimeout ? 24 : Math.max(18, Math.round((latency / maxLatency) * 150));
    bar.className = `hop-bar${hop.isTimeout ? " timeout" : ""}${hop.suspectedSpike ? " spike" : ""}`;
    bar.style.height = `${height}px`;
    bar.dataset.hop = hop.hopNumber;
    bar.title = `Hop ${hop.hopNumber}: ${hop.address} ${latency ? `${latency} ms` : "timeout"}`;
    chart.appendChild(bar);
  });
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
