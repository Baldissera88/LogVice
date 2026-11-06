# Vector versions of the two LogVice icons on a 1024 unit canvas: app() is the app icon, doc() the icon for log files.
NAVY, CYAN, PINK = '#011133', '#00EEFE', '#FF199A'

def at_least(size, base, table, scale=1.0):
    # base = width in canvas units; table = minimum width in screen pixels per icon size
    px = table.get(size, 0)
    return max(base, px * 1024.0 / size / scale)

def app(size):
    w = at_least(size, 36, {16: 1.3, 20: 1.4, 24: 1.5, 32: 1.6, 40: 1.8, 48: 1.9, 64: 2.3})
    lens = 1.0 if size > 24 else 1.12
    lines = [(155, 444, 358), (155, 412, 483), (155, 358, 608)] if size > 16 else [(155, 444, 400), (155, 380, 590)]
    cx, cy, r = 642, 462, 150 * lens
    if size <= 20: cx, cy = 652, 470
    hx0, hy0 = cx + (r + w * 0.9) * 0.707, cy + (r + w * 0.9) * 0.722
    hx1, hy1 = hx0 + 118 * lens, hy0 + 126 * lens
    parts = ['<rect x="20" y="20" width="984" height="984" rx="150" fill="%s"/>' % NAVY]
    for x0, x1, y in lines:
        parts.append('<line x1="%g" y1="%g" x2="%g" y2="%g" stroke="%s" stroke-width="%g" stroke-linecap="round"/>' % (x0 + w / 2, y, x1, y, CYAN, w))
    parts.append('<circle cx="%g" cy="%g" r="%g" fill="none" stroke="%s" stroke-width="%g"/>' % (cx, cy, r, PINK, w))
    parts.append('<line x1="%g" y1="%g" x2="%g" y2="%g" stroke="%s" stroke-width="%g" stroke-linecap="round"/>' % (hx0, hy0, hx1, hy1, CYAN, w))
    return wrap(parts)

def doc(size):
    # document 624 x 790 in the source, scaled 1.2 about the centre
    edge = at_least(size, 9, {16: 1.0, 20: 1.0, 24: 1.0, 32: 1.1, 40: 1.2, 48: 1.3, 64: 1.5}, 1.2)
    g = '<g transform="translate(512 512) scale(1.2) translate(-512 -504)">'
    outline = 'M 254 109 H 622 L 823 310 V 846 Q 823 898 771 898 H 252 Q 200 898 200 846 V 161 Q 200 109 252 109 Z'
    fold = 'M 622 109 V 258 Q 622 310 674 310 H 823'
    parts = [g,
        '<path d="%s" fill="%s" stroke="%s" stroke-width="%g" stroke-linejoin="round"/>' % (outline, NAVY, CYAN, edge),
        '<path d="%s" fill="none" stroke="%s" stroke-width="%g" stroke-linejoin="round"/>' % (fold, CYAN, edge)]
    bh = at_least(size, 34, {16: 1.2, 20: 1.3, 24: 1.4, 32: 1.5, 40: 1.7, 48: 1.8, 64: 2.1}, 1.2)
    if size > 20:
        parts.append('<rect x="297" y="%g" width="272" height="%g" rx="%g" fill="%s"/>' % (379 - bh / 2, bh, bh * 0.25, CYAN))
        parts.append('<rect x="297" y="%g" width="421" height="%g" rx="%g" fill="%s"/>' % (482 - bh / 2, bh, bh * 0.25, PINK))
    else:
        parts.append('<rect x="297" y="%g" width="300" height="%g" rx="%g" fill="%s"/>' % (390 - bh / 2, bh, bh * 0.25, CYAN))
        parts.append('<rect x="297" y="%g" width="421" height="%g" rx="%g" fill="%s"/>' % (540 - bh / 2, bh, bh * 0.25, PINK))
    mw = at_least(size, 22, {16: 1.1, 20: 1.2, 24: 1.3, 32: 1.4, 40: 1.5, 48: 1.6, 64: 1.9}, 1.2)
    cx, cy, r = (587, 660, 74) if size > 20 else (575, 700, 96)
    parts.append('<circle cx="%g" cy="%g" r="%g" fill="none" stroke="%s" stroke-width="%g"/>' % (cx, cy, r, PINK, mw))
    k = (r + mw * 0.5) * 0.707
    parts.append('<line x1="%g" y1="%g" x2="%g" y2="%g" stroke="%s" stroke-width="%g" stroke-linecap="round"/>' % (cx + k, cy + k, cx + k + 62, cy + k + 62, PINK, mw * 1.15))
    parts.append('</g>')
    return wrap(parts)

def wrap(parts):
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024">' + ''.join(parts) + '</svg>'
