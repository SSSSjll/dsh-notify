// dsh-notify 浏览器半侧：在「设置 → 完成提醒」里提供开关与文案编辑。
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

    const box = { display: 'flex', flexDirection: 'column', gap: '14px', padding: '4px 2px' }
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

    function Section() {
      const [cfg, setCfg] = useState(null)
      const [msg, setMsg] = useState('')

      useEffect(() => {
        fetch('/dsh-notify/config')
          .then((r) => r.json())
          .then((j) => { if (j && j.config) setCfg(j.config) })
          .catch(() => setMsg('\u63d0\u9192\u670d\u52a1\u672a\u5c31\u7eea'))
      }, [])

      const set = (k, v) => setCfg((c) => ({ ...c, [k]: v }))

      const fileRef = useRef(null)

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
          h('span', { style: lab }, '\u5bbd\u5ea6'),
          h('input', { style: { ...inp, maxWidth: '110px' }, value: cfg.width || '', onChange: (e) => set('width', e.target.value) }),
          h('span', { style: { ...lab, minWidth: '40px' } }, '\u9ad8\u5ea6'),
          h('input', { style: { ...inp, maxWidth: '110px' }, value: cfg.height || '', onChange: (e) => set('height', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u6ed1\u5165 ms'),
          h('input', { style: { ...inp, maxWidth: '110px' }, value: cfg.anim_in || '', onChange: (e) => set('anim_in', e.target.value) }),
          h('span', { style: { ...lab, minWidth: '40px' } }, '\u6ed1\u51fa ms'),
          h('input', { style: { ...inp, maxWidth: '110px' }, value: cfg.anim_out || '', onChange: (e) => set('anim_out', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u4f4d\u7f6e'),
          h('select', {
            style: { ...inp, maxWidth: '150px' },
            value: cfg.pos || 'br',
            onChange: (e) => set('pos', e.target.value),
            children: [
              h('option', { value: 'br', children: '\u53f3\u4e0b' }),
              h('option', { value: 'bl', children: '\u5de6\u4e0b' }),
              h('option', { value: 'tr', children: '\u53f3\u4e0a' }),
              h('option', { value: 'tl', children: '\u5de6\u4e0a' }),
            ],
          }),
          h('span', { style: { ...lab, minWidth: '40px' } }, '\u8fb9\u8ddd X'),
          h('input', { style: { ...inp, maxWidth: '80px' }, value: cfg.margin_x || '0', onChange: (e) => set('margin_x', e.target.value) }),
          h('span', { style: { ...lab, minWidth: '30px' } }, 'Y'),
          h('input', { style: { ...inp, maxWidth: '80px' }, value: cfg.margin_y || '0', onChange: (e) => set('margin_y', e.target.value) }),
        ),
        h('div', { style: row },
          h('span', { style: lab }, '\u63d2\u56fe'),
          h('button', { style: btn, onClick: pickImage }, cfg.image ? '\u66f4\u6362\u56fe\u7247' : '\u9009\u62e9\u56fe\u7247\u2026'),
          h('span', {
            style: { fontSize: '12px', opacity: 0.6, flex: 1, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
          }, cfg.image ? String(cfg.image).split(/[\\/]/).pop() : '\u672a\u9009\u62e9'),
          cfg.image ? h('button', { style: btn, onClick: clearImage }, '\u6e05\u9664') : null,
          h('span', { style: { ...lab, minWidth: '40px' } }, '\u5c3a\u5bf8'),
          h('input', { style: { ...inp, maxWidth: '70px' }, value: cfg.image_size || '56', onChange: (e) => set('image_size', e.target.value) }),
          h('input', { type: 'file', accept: 'image/*', ref: fileRef, onChange: onFile, style: { display: 'none' } }),
        ),
        h('div', { style: { ...row, justifyContent: 'flex-end' } },
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
