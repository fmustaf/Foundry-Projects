const messages = document.querySelector('#messages');
const input = document.querySelector('#input');
const form = document.querySelector('#form');
const history = [];
function add(role, text) { const div=document.createElement('div'); div.className=`msg ${role==='user'?'user':'bot'}`; div.textContent=text; messages.appendChild(div); messages.scrollTop=messages.scrollHeight; }
async function send(text) {
  const value=(text||input.value).trim(); if(!value) return;
  add('user',value); input.value='';
  const loading=document.createElement('div'); loading.className='msg bot'; loading.textContent='Checking hotel information...'; messages.appendChild(loading);
  try {
    const res=await fetch('/api/chat',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({message:value,history})});
    const data=await res.json(); loading.remove();
    if(!res.ok) throw new Error(data.detail||data.error||'Request failed');
    add('assistant',data.answer); history.push({role:'user',content:value},{role:'assistant',content:data.answer});
  } catch(e) { loading.remove(); add('assistant',`Unable to contact the AI service: ${e.message}`); }
}
form.addEventListener('submit',e=>{e.preventDefault();send()});
document.querySelectorAll('.chips button').forEach(b=>b.onclick=()=>send(b.textContent));
