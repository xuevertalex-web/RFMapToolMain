const webviewClientTimelineRenderer = `function renderRunTimeline(run) {
        if (!timelineList || !timelineEmpty) return;
        timelineList.replaceChildren();
        if (!Array.isArray(run.timeline) || run.timeline.length === 0) {
          timelineEmpty.style.display = 'block';
          return;
        }
        timelineEmpty.style.display = 'none';
        for (const event of run.timeline) {
          const item = document.createElement('li');
          item.className = 'timeline-item';
          const title = document.createElement('div');
          title.className = 'timeline-title';
          title.textContent = event.stage + ' · ' + event.status;
          const detail = document.createElement('div');
          detail.className = 'timeline-detail';
          detail.textContent = [event.timestamp, event.message].filter(Boolean).join(' — ') || 'not available';
          item.appendChild(title);
          item.appendChild(detail);
          timelineList.appendChild(item);
        }
      }`;

function getWebviewClientTimelineRenderer() {
  return webviewClientTimelineRenderer;
}

module.exports = { getWebviewClientTimelineRenderer };
