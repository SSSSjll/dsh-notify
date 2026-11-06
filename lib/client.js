// dsh-notify 浏览器半侧：设置里的「完成提醒」面板 + 位置编辑页。
window.__ModuleLoader__.load({
  id: 'dsh-finish-notify',
  factory: function (require) {
    const React = require('react')
    const { useEffect, useState, useRef } = React
    const { jsx: j } = require('react/jsx-runtime')
    // jsx(type, props, key) —— children 必须放进 props.children，第三参是 key
    const h = function (type, props) {
      const kids = Array.prototype.slice.call(arguments, 2)
      const p = Object.assign({}, props || {})
      if (kids.length === 1) p.children = kids[0]
      else if (kids.length > 1) p.children = kids
      return j(type, p)
    }

    const box = { display: 'flex', flexDirection: 'column', gap: '12px', padding: '4px 2px' }
    const row = { display: 'flex', alignItems: 'center', gap: '10px' }
    const lab = { fontSize: '13px', opacity: 0.72, minWidth: '64px' }
    const inp = {
      flex: 1, padding: '7px 10px', borderRadius: '8px',
      border: '1px solid rgba(128,128,128,0.35)', background: 'transparent',
      color: 'inherit', fontSize: '13px', outline: 'none',
    }
    const btn = {
      padding: '7px 14px', borderRadius: '8px', cursor: 'pointer', fontSize: '13px',
      border: '1px solid rgba(128,128,128,0.35)', background: 'transparent', color: 'inherit',
    }
    const hint = { fontSize: '12px', opacity: 0.6, flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }

    const FONTS = [
      'Microsoft YaHei UI', 'Microsoft YaHei', 'SimHei', 'SimSun', 'KaiTi',
      'FangSong', 'Segoe UI', 'Arial', 'Consolas', 'Cascadia Code',
    ]

    function Switch(props) {
      const on = props.value
      return h(
        'div',
        {
          onClick: () => props.onChange(!on),
          style: {
            width: '40px', height: '22px', borderRadius: '11px', cursor: 'pointer',
            background: on ? '#2f7d32' : 'rgba(128,128,128,0.4)',
            position: 'relative', transition: 'background .18s',
          },
        },
        h('div', {
          style: {
            position: 'absolute', top: '3px', left: on ? '21px' : '3px',
            width: '16px', height: '16px', borderRadius: '50%', background: '#fff',
            transition: 'left .18s',
          },
        }),
      )
    }

    // ── 文字位置编辑页：在框内拖动文字 + 方向键 ──────────────
    function TextEditor(props) {
      const cfg = props.cfg
      const setCfg = props.setCfg
      const wrap = useRef(null)
      const dragging = useRef(false)
      const startPt = useRef({ x: 0, y: 0, tx: 0, ty: 0 })

      const W = Number(cfg.width || 336) || 336
      const H = Number(cfg.height || 124) || 124
      const k = 440 / W
      const baseX = (cfg.img_pos === 'left' && cfg.image)
        ? 14 + (Number(cfg.image_size) || 56) + 10 : 14
      const tx = Number(cfg.text_x) >= 0 ? Number(cfg.text_x) : baseX
      const ty = Number(cfg.text_y) >= 0 ? Number(cfg.text_y) : 12
      const textW = Math.max(40, W - tx - 14)
      const align = cfg.align === 'center' ? 'center' : (cfg.align === 'right' ? 'right' : 'left')
      const fontFam = "'" + (cfg.font || 'Microsoft YaHei UI') + "', sans-serif"

      const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v))

      const onDown = (e) => {
        dragging.current = true
        startPt.current = { x: e.clientX, y: e.clientY, tx: tx, ty: ty }
        e.preventDefault()
        e.stopPropagation()
      }
      const onMove = (e) => {
        if (!dragging.current || !wrap.current) return
        const r = wrap.current.getBoundingClientRect()
        const dx = (e.clientX - startPt.current.x) / r.width * W
        const dy = (e.clientY - startPt.current.y) / r.height * H
        setCfg((c) => Object.assign({}, c, {
          text_x: String(Math.round(clamp(startPt.current.tx + dx, 0, W - 24))),
          text_y: String(Math.round(clamp(startPt.current.ty + dy, 0, H - 16))),
        }))
      }
      const onUp = () => { dragging.current = false }

      const onKey = (e) => {
        const step = e.shiftKey ? 10 : 1
        let nx = tx
        let ny = ty
        if (e.key === 'ArrowRight') nx += step
        else if (e.key === 'ArrowLeft') nx -= step
        else if (e.key === 'ArrowDown') ny += step
        else if (e.key === 'ArrowUp') ny -= step
        else return
        e.preventDefault()
        setCfg((c) => Object.assign({}, c, {
          text_x: String(clamp(nx, 0, W - 24)),
          text_y: String(clamp(ny, 0, H - 16)),
        }))
      }

      return h('div', { style: box },
        h('div', { style: row },
          h('span', { style: { fontSize: '13px', fontWeight: 600 } }, '\u8c03\u6574\u6587\u5b57\u4f4d\u7f6e'),
          h('span', { style: hint }, '\u62d6\u52a8\u6846\u5185\u6587\u5b57\uff0c\u6216\u70b9\u4e00\u4e0b\u518d\u7528\u65b9\u5411\u952e'),
        ),
        h('div', {
          tabIndex: 0,
          onKeyDown: onKey,
          onMouseMove: onMove,
          onMouseUp: onUp,
          onMouseLeave: onUp,
          style: {
            outline: 'none', padding: '26px 0',
            background: 'linear-gradient(180deg,#eef0f3,#e6e8ec)',
            borderRadius: '10px', border: '1px solid rgba(128,128,128,0.25)',
          },
        },
          h('div', {
            ref: wrap,
            style: {
              position: 'relative',
              width: (W * k) + 'px', height: (H * k) + 'px',
              margin: '0 auto', background: '#fff',
              border: '1px solid #dedede', borderRadius: '6px',
              boxSizing: 'border-box', overflow: 'hidden',
            },
          },
            h('div', {
              onMouseDown: onDown,
              style: {
                position: 'absolute',
                left: (tx * k) + 'px', top: (ty * k) + 'px',
                width: (textW * k) + 'px',
                cursor: 'grab', userSelect: 'none',
              },
            },
              h('div', {
                style: {
                  fontFamily: fontFam,
                  fontSize: Math.max(11, (Number(cfg.title_size) || 11.5) * 1.6) + 'px',
                  fontWeight: 700, color: '#141414', textAlign: align,
                  whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
                },
              }, cfg.title || '\u2714  \u8bf7\u6c42\u5df2\u5b8c\u6210'),
              h('div', {
                style: {
                  fontFamily: fontFam,
                  fontSize: Math.max(9, (Number(cfg.sub_size) || 9) * 1.5) + 'px',
                  color: '#787878', marginTop: '3px', textAlign: align,
                  whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
                },
              }, cfg.sub || ''),
            ),
          ),
        ),
        h('div', { style: row },
          h('span', { style: lab }, 'X'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '90px' }), value: tx, onChange: (e) => setCfg((c) => Object.assign({}, c, { text_x: e.target.value })) }),
          h('span', { style: lab }, 'Y'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '90px' }), value: ty, onChange: (e) => setCfg((c) => Object.assign({}, c, { text_y: e.target.value })) }),
          h('span', { style: hint }, '\u65b9\u5411\u952e 1px\uff0cShift+ \u65b9\u5411\u952e 10px'),
        ),
        h('div', { style: Object.assign({}, row, { justifyContent: 'flex-end' }) },
          h('button', { style: btn, onClick: () => { props.onSave(); props.onBack() } }, '\u4fdd\u5b58\u5e76\u8fd4\u56de'),
          h('button', { style: btn, onClick: props.onBack }, '\u8fd4\u56de'),
        ),
      )
    }

    // ── 位置编辑页：拖动 + 方向键 ──────────────────────────────
    function PosEditor(props) {
      const cfg = props.cfg
      const setCfg = props.setCfg
      const wrap = useRef(null)
      const dragging = useRef(false)
      const screenRef = useRef({ w: 2560, h: 1600 })

      const sw = screenRef.current.w
      const sh = screenRef.current.h
      const pos = String(cfg.pos || 'br')
      const mx = Number(cfg.margin_x || 0) || 0
      const my = Number(cfg.margin_y || 0) || 0
      const bw = Number(cfg.width || 336) || 336
      const bh = Number(cfg.height || 124) || 124

      const apply = (e) => {
        const el = wrap.current
        if (!el) return
        const r = el.getBoundingClientRect()
        const rx = Math.min(1, Math.max(0, (e.clientX - r.left) / r.width))
        const ry = Math.min(1, Math.max(0, (e.clientY - r.top) / r.height))
        const left = rx < 0.5
        const top = ry < 0.5
        const next = (top ? 't' : 'b') + (left ? 'l' : 'r')
        const nmx = Math.round(left ? rx * sw : (1 - rx) * sw)
        const nmy = Math.round(top ? ry * sh : (1 - ry) * sh)
        setCfg((c) => Object.assign({}, c, {
          pos: next, margin_x: String(nmx), margin_y: String(nmy),
        }))
      }

      const onDown = (e) => { dragging.current = true; apply(e) }
      const onMove = (e) => { if (dragging.current) apply(e) }
      const onUp = () => { dragging.current = false }

      const onKey = (e) => {
        const step = e.shiftKey ? 10 : 1
        let nmx = mx
        let nmy = my
        if (e.key === 'ArrowRight') nmx += pos.startsWith('r') ? -step : step
        else if (e.key === 'ArrowLeft') nmx += pos.startsWith('r') ? step : -step
        else if (e.key === 'ArrowDown') nmy += pos.endsWith('b') ? -step : step
        else if (e.key === 'ArrowUp') nmy += pos.endsWith('b') ? step : -step
        else return
        e.preventDefault()
        setCfg((c) => Object.assign({}, c, {
          margin_x: String(Math.max(0, nmx)), margin_y: String(Math.max(0, nmy)),
        }))
      }

      const corner = (p, label) => h('button', {
        style: Object.assign({}, btn, {
          padding: '6px 10px',
          background: pos === p ? 'rgba(47,125,50,0.14)' : 'transparent',
          borderColor: pos === p ? 'rgba(47,125,50,0.6)' : 'rgba(128,128,128,0.35)',
        }),
        onClick: () => setCfg((c) => Object.assign({}, c, { pos: p })),
      }, label)

      const toastStyle = Object.assign({
        position: 'absolute',
        width: (bw / sw * 100) + '%',
        height: (bh / sh * 100) + '%',
        background: '#fff',
        border: '1px solid #dedede',
        borderRadius: '6px',
        boxSizing: 'border-box',
        cursor: 'grab',
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        padding: '0 8px',
        userSelect: 'none',
      }, pos.startsWith('r') ? { right: (mx / sw * 100) + '%' } : { left: (mx / sw * 100) + '%' },
         pos.endsWith('b') ? { bottom: (my / sh * 100) + '%' } : { top: (my / sh * 100) + '%' })

      return h('div', { style: box },
        h('div', { style: row },
          h('span', { style: { fontSize: '13px', fontWeight: 600 } }, '\u8c03\u6574\u4f4d\u7f6e'),
          h('span', { style: hint }, '\u62d6\u52a8\u767d\u6846\uff0c\u6216\u70b9\u4e0b\u65b9\u518d\u7528\u65b9\u5411\u952e\u5fae\u8c03'),
        ),
        h('div', {
          ref: wrap,
          tabIndex: 0,
          onKeyDown: onKey,
          onMouseDown: onDown,
          onMouseMove: onMove,
          onMouseUp: onUp,
          onMouseLeave: onUp,
          style: {
            position: 'relative', width: '100%',
            aspectRatio: sw + ' / ' + sh,
            background: 'linear-gradient(180deg,#eef0f3,#e6e8ec)',
            borderRadius: '10px', overflow: 'hidden', outline: 'none',
            border: '1px solid rgba(128,128,128,0.25)', cursor: 'crosshair',
          },
        },
          h('div', { style: toastStyle },
            cfg.image
              ? h('div', {
                style: {
                  width: '18px', height: '18px', borderRadius: '4px',
                  background: '#c9ccd2', marginBottom: '4px',
                  display: cfg.img_pos === 'top' ? 'block' : 'none',
                },
              })
              : null,
            h('div', {
              style: {
                fontFamily: "'" + (cfg.font || 'Microsoft YaHei UI') + "', sans-serif",
                fontSize: Math.max(10, (Number(cfg.title_size) || 11.5) * 1.5) + 'px',
                fontWeight: 700, color: '#141414',
                textAlign: cfg.align === 'center' ? 'center' : (cfg.align === 'right' ? 'right' : 'left'),
                whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
              },
            }, cfg.title || '\u2714  \u8bf7\u6c42\u5df2\u5b8c\u6210'),
            h('div', {
              style: {
                fontFamily: "'" + (cfg.font || 'Microsoft YaHei UI') + "', sans-serif",
                fontSize: Math.max(8, (Number(cfg.sub_size) || 9) * 1.4) + 'px',
                color: '#787878', marginTop: '4px',
                textAlign: cfg.align === 'center' ? 'center' : (cfg.align === 'right' ? 'right' : 'left'),
                whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
              },
            }, cfg.sub || ''),
          ),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u5b9a\u4f4d'),
          corner('tl', '\u5de6\u4e0a'), corner('tr', '\u53f3\u4e0a'),
          corner('bl', '\u5de6\u4e0b'), corner('br', '\u53f3\u4e0b'),
          h('span', { style: hint }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u8fb9\u8ddd'),
          h('span', { style: { fontSize: '12px', opacity: 0.6 } }, 'X'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '90px' }), value: cfg.margin_x || '0', onChange: (e) => setCfg((c) => Object.assign({}, c, { margin_x: e.target.value })) }),
          h('span', { style: { fontSize: '12px', opacity: 0.6 } }, 'Y'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '90px' }), value: cfg.margin_y || '0', onChange: (e) => setCfg((c) => Object.assign({}, c, { margin_y: e.target.value })) }),
          h('span', { style: hint }, '\u5e26 Shift \u6309 10px \u6b65\u8fdb'),
        ),
        h('div', { style: Object.assign({}, row, { justifyContent: 'flex-end' }) },
          h('button', { style: btn, onClick: () => { props.onSave(); props.onBack() } }, '\u4fdd\u5b58\u5e76\u8fd4\u56de'),
          h('button', { style: btn, onClick: props.onBack }, '\u8fd4\u56de'),
        ),
      )
    }

    // ── 主面板 ────────────────────────────────────────────────
    function Section() {
      const [cfg, setCfg] = useState(null)
      const [msg, setMsg] = useState('')
      const [view, setView] = useState('main')
      const fileRef = useRef(null)

      useEffect(() => {
        fetch('/dsh-notify/config')
          .then((r) => r.json())
          .then((j) => { if (j && j.config) setCfg(j.config) })
          .catch(() => setMsg('\u63d0\u9192\u670d\u52a1\u672a\u5c31\u7eea'))
      }, [])

      const set = (k, v) => setCfg((c) => Object.assign({}, c, { [k]: v }))

      const pickImage = () => { if (fileRef.current) fileRef.current.click() }

      const onFile = (e) => {
        const f = e.target.files && e.target.files[0]
        if (!f) return
        setMsg('\u4e0a\u4f20\u4e2d\u2026')
        fetch('/dsh-notify/image?name=' + encodeURIComponent(f.name), {
          method: 'POST',
          headers: { 'content-type': 'application/octet-stream' },
          body: f,
        })
          .then((r) => r.json())
          .then((j) => {
            if (j && j.ok) { setCfg((c) => Object.assign({}, c, { image: j.path })); setMsg('\u63d2\u56fe\u5df2\u66f4\u65b0') }
            else setMsg('\u4e0a\u4f20\u5931\u8d25')
          })
          .catch(() => setMsg('\u4e0a\u4f20\u5931\u8d25'))
        e.target.value = ''
      }

      const clearImage = () => {
        fetch('/dsh-notify/image/clear', { method: 'POST' })
          .then((r) => r.json())
          .then(() => { setCfg((c) => Object.assign({}, c, { image: '' })); setMsg('\u5df2\u6e05\u9664\u63d2\u56fe') })
          .catch(() => setMsg('\u6e05\u9664\u5931\u8d25'))
      }

      const save = () => {
        fetch('/dsh-notify/config', {
          method: 'POST',
          headers: { 'content-type': 'application/json' },
          body: JSON.stringify(cfg),
        })
          .then((r) => r.json())
          .then(() => setMsg('\u5df2\u4fdd\u5b58'))
          .catch(() => setMsg('\u4fdd\u5b58\u5931\u8d25'))
      }

      const test = () => {
        fetch('/dsh-notify/test', { method: 'POST' })
          .then(() => setMsg('\u5df2\u53d1\u9001\u6d4b\u8bd5'))
          .catch(() => setMsg('\u6d4b\u8bd5\u5931\u8d25'))
      }

      if (!cfg) return h('div', { style: { fontSize: '13px', opacity: 0.7 } }, msg || '\u52a0\u8f7d\u4e2d\u2026')

      if (view === 'pos') {
        return h(PosEditor, {
          cfg, setCfg, onSave: save, onBack: () => setView('main'),
        })
      }

      if (view === 'text') {
        return h(TextEditor, {
          cfg, setCfg, onSave: save, onBack: () => setView('main'),
        })
      }

      return h('div', { style: box },
        h('div', { style: row },
          h('span', { style: lab }, '\u542f\u7528'),
          h(Switch, { value: String(cfg.enabled) !== '0', onChange: (v) => set('enabled', v ? '1' : '0') }),
          h('span', { style: { fontSize: '12px', opacity: 0.55 } }, '\u968f DSH \u542f\u52a8\u5173\u95ed'),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u6807\u9898'),
          h('input', { style: inp, value: cfg.title || '', onChange: (e) => set('title', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u526f\u6807\u9898'),
          h('input', { style: inp, value: cfg.sub || '', onChange: (e) => set('sub', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u5b57\u4f53'),
          h('input', {
            style: inp, value: cfg.font || '', list: 'dsh-notify-fonts',
            placeholder: 'Microsoft YaHei UI',
            onChange: (e) => set('font', e.target.value),
          }),
          h('datalist', {
            id: 'dsh-notify-fonts',
            children: FONTS.map((f) => h('option', { key: f, value: f })),
          }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u5b57\u53f7'),
          h('span', { style: { fontSize: '12px', opacity: 0.6 } }, '\u6807\u9898'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '80px' }), value: cfg.title_size || '11.5', onChange: (e) => set('title_size', e.target.value) }),
          h('span', { style: { fontSize: '12px', opacity: 0.6 } }, '\u526f\u6807\u9898'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '80px' }), value: cfg.sub_size || '9', onChange: (e) => set('sub_size', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u5bf9\u9f50'),
          h('select', {
            style: Object.assign({}, inp, { maxWidth: '120px' }),
            value: cfg.align || 'left',
            onChange: (e) => set('align', e.target.value),
            children: [
              h('option', { value: 'left', children: '\u5de6' }),
              h('option', { value: 'center', children: '\u4e2d' }),
              h('option', { value: 'right', children: '\u53f3' }),
            ],
          }),
          h('span', { style: Object.assign({}, lab, { minWidth: '40px' }) }, '\u6392\u5217'),
          h('select', {
            style: Object.assign({}, inp, { maxWidth: '140px' }),
            value: cfg.img_pos || 'left',
            onChange: (e) => set('img_pos', e.target.value),
            children: [
              h('option', { value: 'left', children: '\u56fe\u6807\u5728\u5de6' }),
              h('option', { value: 'right', children: '\u56fe\u6807\u5728\u53f3' }),
              h('option', { value: 'top', children: '\u56fe\u6807\u5728\u4e0a' }),
            ],
          }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u6d88\u5931\u6548\u679c'),
          h('select', {
            style: Object.assign({}, inp, { maxWidth: '210px' }),
            value: cfg.exit_fx || 'auto',
            onChange: (e) => set('exit_fx', e.target.value),
            children: [
              h('option', { value: 'auto', children: '\u81ea\u52a8\uff08\u8d34\u8fb9\u6ed1\u51fa\uff0c\u5426\u5219\u7070\u98de\u6e6e\u706d\uff09' }),
              h('option', { value: 'slide', children: '\u59cb\u7ec8\u6ed1\u51fa' }),
              h('option', { value: 'dissolve', children: '\u7070\u98de\u6e6d\u706d' }),
              h('option', { value: 'rise', children: '\u98de\u5347' }),
              h('option', { value: 'collapse', children: '\u5012\u584c' }),
              h('option', { value: 'hand', children: '\u6076\u9b54\u624b' }),
              h('option', { value: 'wave', children: '\u51b2\u51fb\u6ce2\u62cd\u788e' }),
              h('option', { value: 'random', children: '\u968f\u673a\uff08\u6bcf\u79cd\u7b49\u6982\u7387\uff09' }),
            ],
          }),
          h('button', { style: btn, onClick: () => fetch('/dsh-notify/test?fx=' + encodeURIComponent(cfg.exit_fx || 'auto'), { method: 'POST' }) }, '\u9884\u89c8'),
          h('span', { style: Object.assign({}, lab, { minWidth: '40px' }) }, '\u65f6\u957f'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '90px' }), value: cfg.fx_ms || '800', onChange: (e) => set('fx_ms', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u5bbd\u5ea6'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '110px' }), value: cfg.width || '', onChange: (e) => set('width', e.target.value) }),
          h('span', { style: Object.assign({}, lab, { minWidth: '40px' }) }, '\u9ad8\u5ea6'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '110px' }), value: cfg.height || '', onChange: (e) => set('height', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u6ed1\u5165 ms'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '110px' }), value: cfg.anim_in || '', onChange: (e) => set('anim_in', e.target.value) }),
          h('span', { style: Object.assign({}, lab, { minWidth: '40px' }) }, '\u6ed1\u51fa ms'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '110px' }), value: cfg.anim_out || '', onChange: (e) => set('anim_out', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u5c4f\u5e55\u4f4d\u7f6e'),
          h('button', { style: btn, onClick: () => setView('pos') }, '\u8c03\u6574\u2026'),
          h('span', { style: hint }, (String(cfg.pos || 'br') === 'br' ? '\u53f3\u4e0b' : String(cfg.pos) === 'bl' ? '\u5de6\u4e0b' : String(cfg.pos) === 'tr' ? '\u53f3\u4e0a' : '\u5de6\u4e0a') + '\u00a0\u00a0\u8fb9\u8ddd ' + (cfg.margin_x || '0') + ', ' + (cfg.margin_y || '0')),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u6587\u5b57\u4f4d\u7f6e'),
          h('button', { style: btn, onClick: () => setView('text') }, '\u8c03\u6574\u2026'),
          h('span', { style: hint }, 'X=' + (Number(cfg.text_x) >= 0 ? cfg.text_x : '\u81ea\u52a8') + '\u00a0\u00a0Y=' + (Number(cfg.text_y) >= 0 ? cfg.text_y : '\u81ea\u52a8')),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u63d2\u56fe'),
          h('button', { style: btn, onClick: pickImage }, cfg.image ? '\u66f4\u6362\u56fe\u7247' : '\u9009\u62e9\u56fe\u7247\u2026'),
          h('span', { style: hint }, cfg.image ? String(cfg.image).split(/[\\/]/).pop() : '\u672a\u9009\u62e9'),
          cfg.image ? h('button', { style: btn, onClick: clearImage }, '\u6e05\u9664') : null,
          h('span', { style: Object.assign({}, lab, { minWidth: '40px' }) }, '\u5c3a\u5bf8'),
          h('input', { style: Object.assign({}, inp, { maxWidth: '70px' }), value: cfg.image_size || '56', onChange: (e) => set('image_size', e.target.value) }),
          h('input', { type: 'file', accept: 'image/*', ref: fileRef, onChange: onFile, style: { display: 'none' } }),
        ),
        h('div', { style: Object.assign({}, row, { justifyContent: 'flex-end' }) },
          msg ? h('span', { style: { fontSize: '12px', opacity: 0.6, marginRight: 'auto' } }, msg) : null,
          h('button', { style: btn, onClick: test }, '\u6d4b\u8bd5'),
          h('button', { style: btn, onClick: save }, '\u4fdd\u5b58'),
        ),
      )
    }

    const name = 'dsh-finish-notify'
    const inject = ['slots']

    function apply(ctx) {
      ctx.slots.inject('settings.section', function* () {
        yield ctx.slots.register(
          { name: 'settings.section', id: 'dsh-finish-notify-config', order: 60, label: () => '\u5b8c\u6210\u63d0\u9192' },
          Section,
        )
      })
    }

    const module = { exports: {} }
    module.exports = { apply, inject, name }
    return module.exports
  },
})