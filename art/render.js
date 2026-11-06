const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const jobs = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
  const b = await chromium.launch();
  const p = await b.newPage({ deviceScaleFactor: 1 });
  for (const j of jobs) {
    await p.setViewportSize({ width: j.size, height: j.size });
    await p.setContent('<html><body style="margin:0;background:transparent">' + j.svg.replace('<svg ', '<svg width="' + j.size + '" height="' + j.size + '" style="display:block" ') + '</body></html>');
    await p.screenshot({ path: j.out, omitBackground: true, clip: { x: 0, y: 0, width: j.size, height: j.size } });
  }
  await b.close();
})();
