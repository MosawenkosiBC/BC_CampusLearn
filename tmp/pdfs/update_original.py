from pathlib import Path
import ast
from datetime import date,timedelta
from reportlab.pdfgen import canvas
from reportlab.lib.utils import simpleSplit
from reportlab.lib import colors
from pypdf import PdfReader

root=Path(__file__).resolve().parents[2]
old=root/'output/pdf/Belgium_Campus_October_2026_Logsheet.pdf'
fields=PdfReader(old).get_fields()
tree=ast.parse((root/'tmp/pdfs/complete_october.py').read_text())
tasks=next(ast.literal_eval(n.value) for n in tree.body if isinstance(n,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='tasks' for t in n.targets))
out=root/'output/pdf/Belgium_Campus_October_2026_Original_Form.pdf'
c=canvas.Canvas(str(out),pagesize=(843,597))
c.setTitle('Belgium Campus - October 2026 - Original Form Updated')
def white(x,t,w,h):
 c.setFillColor(colors.white);c.rect(x,597-t-h,w,h,fill=1,stroke=0)
def text(x,t,s,size=10):
 c.setFillColor(colors.black);c.setFont('Helvetica',size);c.drawString(x,597-t-size,s)
def bg(n):c.drawImage(str(root/f'tmp/pdfs/original-hi-{n}.png'),0,0,width=843,height=597)
bg(1)
white(244,251,522,36);text(260,263,'30/10/2026',12)
c.showPage()
bg(2)
bounds=[153.26,168.14,183.05,197.81,212.69,227.57,242.57,257.45,270.29,285.29,300.31,315.07,329.95,344.71,359.59,374.59,389.47,404.35,419.14,433.9,448.78,463.78,478.66,493.66]
entries=list(tasks.items())
for i,(top,bottom) in enumerate(zip(bounds,bounds[1:])):
 white(73.3,top+.8,118.7,bottom-top-1.5);white(193.8,top+.8,567.2,bottom-top-1.5)
 if i<len(entries):
  d,desc=entries[i];text(75,top+3,f'{d:02d}/10/2026',7)
  lines=simpleSplit(desc,'Helvetica',6.7,560)
  for j,line in enumerate(lines):text(196,top+1.8+j*7,line,6.7)
c.showPage()
bg(3)
white(71,100,700,180)
comment=(
 'During October, Mosa contributed to the development and improvement of the CampusLearn platform. '
 'His work included tutor administration, application reviews, compensation reporting and real-time announcements. '
 'He also worked on Microsoft Teams integration for booking confirmations, interview scheduling, shared meeting organisation, '
 'recordings and attendance reviews. He improved navigation and investigated application errors, showing a practical approach '
 'to problem-solving and attention to the needs of students, tutors and administrators. His contributions demonstrate continued '
 'growth in software development, system integration and troubleshooting.'
)
for i,line in enumerate(simpleSplit(comment,'Helvetica',10,680)):text(73,103+i*19,line,10)
white(540,292,145,18);text(553,296,'30/10/2026',10)
white(540,353,145,15);text(553,356,'30/10/2026',10)
c.showPage()
bg(4)
# Keep the original signature column and all signature images untouched.
white(255,52,476,12);text(266,52,'Mosa Msiza',10)
white(255,66,476,11);text(260,66,'Estimated times for review; lunch assumed at 12:00.',7)
bounds=[92.3,106.58,120.98,135.38,149.78,164.18,178.61,192.89,207.29,221.69,236.09,250.49,264.89,279.17,293.59,307.99,322.51,336.79,351.19,365.59,379.99,394.27,408.67,423.1,437.5,451.9,466.18]
cols=[111.5,254.4,369.9,494.8,606.5]
white(111.5,92.9,494.5,372.7)
c.setStrokeColor(colors.black);c.setLineWidth(.5)
for top in bounds:c.line(111,597-top,606,597-top)
for x in cols:c.line(x-.3,597-bounds[0],x-.3,597-bounds[-1])
for i,(top,bottom) in enumerate(zip(bounds,bounds[1:])):
 day=date(2026,10,5)+timedelta(days=i)
 text(116,top+1.8,day.strftime('%d/%m/%Y'),9)
 if day.weekday()<5:
  for j,key in enumerate(['time_in','lunch','time_out']):text(cols[j+1]+12,top+1.8,str(fields[f'{key}_{day.isoformat()}']['/V']),10)
 else:text(257,top+2,day.strftime('%A'),8)
c.showPage();c.save()
r=PdfReader(out)
assert len(r.pages)==4
page=r.pages[3].extract_text()
assert '08:07' in page and '16:47' in page and '30/10/2026' in page
print(out)
