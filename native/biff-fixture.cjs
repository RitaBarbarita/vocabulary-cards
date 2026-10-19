// Independent BIFF8 / CFB fixture, including a Unicode SST continued across records.
const fs=require('fs'),path=require('path');
const u16=n=>{const b=Buffer.alloc(2);b.writeUInt16LE(n);return b;},u32=n=>{const b=Buffer.alloc(4);b.writeUInt32LE(n>>>0);return b;};
const rec=(id,data)=>Buffer.concat([u16(id),u16(data.length),data]);
const bof=kind=>rec(0x809,Buffer.concat([u16(0x600),u16(kind),u16(0xDBB),u16(0x7CC),u32(0x41),u32(6)]));
const strings=['Word or Phrase','Translation','Explanation','Antonyms / Opposite','Score','你好','привет','長い説明 '.repeat(2000),'再见'];
let parts=[Buffer.concat([u32(strings.length),u32(strings.length)])],size=8;
const append=b=>{parts[parts.length-1]=Buffer.concat([parts[parts.length-1],b]);size+=b.length;};
for(const s of strings){if(size+3>8224){parts.push(Buffer.alloc(0));size=0;}append(Buffer.concat([u16(s.length),Buffer.from([1])]));const bytes=Buffer.from(s,'utf16le');for(let at=0;at<bytes.length;){let take=Math.min(bytes.length-at,Math.floor((8224-size)/2)*2);if(take===0){parts.push(Buffer.from([1]));size=1;continue;}append(bytes.subarray(at,at+take));at+=take;if(at<bytes.length){parts.push(Buffer.from([1]));size=1;}}}
const sst=Buffer.concat(parts.map((b,i)=>rec(i?0x3c:0xfc,b))),eof=rec(10,Buffer.alloc(0));
let bound=rec(0x85,Buffer.concat([u32(0),Buffer.from([0,0,4,0]),Buffer.from('Data')]));
const offset=bof(5).length+bound.length+sst.length+eof.length;bound.writeUInt32LE(offset,4);
const cell=(r,c,index)=>rec(0xfd,Buffer.concat([u16(r),u16(c),u16(0),u32(index)]));
const cells=[];for(let i=0;i<5;i++)cells.push(cell(0,i,i));for(let i=0;i<4;i++)cells.push(cell(1,i,i+5));cells.push(rec(0x27e,Buffer.concat([u16(1),u16(4),u16(0),u32((1250<<2)|3)])));
const workbook=Buffer.concat([bof(5),bound,sst,eof,bof(0x10),...cells,eof]);
const sectors=Math.ceil(workbook.length/512),fatSector=sectors+1,header=Buffer.alloc(512);
Buffer.from('d0cf11e0a1b11ae1','hex').copy(header);header.writeUInt16LE(0x3e,24);header.writeUInt16LE(3,26);header.writeUInt16LE(0xfffe,28);header.writeUInt16LE(9,30);header.writeUInt16LE(6,32);header.writeUInt32LE(1,44);header.writeUInt32LE(0,48);header.writeUInt32LE(4096,56);header.writeUInt32LE(0xfffffffe,60);header.writeUInt32LE(0xfffffffe,68);for(let i=0;i<109;i++)header.writeUInt32LE(i===0?fatSector:0xffffffff,76+i*4);
const directory=Buffer.alloc(512);function entry(at,name,type,start,length){Buffer.from(name+'\0','utf16le').copy(directory,at);directory.writeUInt16LE((name.length+1)*2,at+64);directory[at+66]=type;directory[at+67]=1;for(const p of [68,72,76])directory.writeUInt32LE(0xffffffff,at+p);directory.writeUInt32LE(start>>>0,at+116);directory.writeUInt32LE(length,at+120);}
entry(0,'Root Entry',5,0xfffffffe,0);directory.writeUInt32LE(1,76);entry(128,'Workbook',2,1,workbook.length);
const data=Buffer.alloc(sectors*512);workbook.copy(data);const fat=Buffer.alloc(512,0xff);fat.writeUInt32LE(0xfffffffe,0);for(let i=1;i<=sectors;i++)fat.writeUInt32LE(i===sectors?0xfffffffe:i+1,i*4);fat.writeUInt32LE(0xfffffffd,fatSector*4);
fs.mkdirSync(path.join(__dirname,'fixtures'),{recursive:true});fs.writeFileSync(path.join(__dirname,'fixtures','excel-biff8.xls'),Buffer.concat([header,directory,data,fat]));console.log('Created BIFF8 fixture ('+workbook.length+' bytes).');
