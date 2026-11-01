from pathlib import Path
from datetime import date, timedelta
from random import Random
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.utils import simpleSplit
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'output/pdf/Belgium_Campus_October_2026_Logsheet.pdf'
OUT.parent.mkdir(parents=True,exist_ok=True)
c=canvas.Canvas(str(OUT),pagesize=(843,597))
c.setTitle('Belgium Campus - October 2026 - Mosa Msiza')
c.setAuthor('Mosa Msiza')
gray=colors.HexColor('#dddddd')

def txt(x,y,s,size=10,bold=False):
 c.setFont('Helvetica-Bold' if bold else 'Helvetica',size);c.setFillColor(colors.black);c.drawString(x,y,s)
def base(n,title):
 c.setLineWidth(1.5);c.rect(27,27,789,543);c.setLineWidth(.4);c.rect(30,30,783,537)
 txt(72,529,title,13,True);txt(72,510,'October 2026 | Reporting period: 05/10/2026 - 30/10/2026',9)
 txt(770,46,str(n),10)
def field(name,x,y,w,h,value='',size=10,multiline=False):
 c.acroForm.textfield(name=name,tooltip=name.replace('_',' '),x=x,y=y,width=w,height=h,value=value,
  fontName='Helvetica',fontSize=size,textColor=colors.black,borderWidth=0,fillColor=colors.white,
  forceBorder=False,fieldFlags='multiline' if multiline else '')
def row(label,value,y,name,height=34):
 c.setFillColor(gray);c.rect(72,y-height,175,height,fill=1,stroke=0)
 c.setStrokeColor(colors.black);c.rect(72,y-height,698,height);c.line(247,y-height,247,y)
 for j,line in enumerate(simpleSplit(label,'Helvetica',10,165)):txt(79,y-15-j*12,line,10)
 field(name,254,y-height+4,509,height-8,value,10)

base(1,'Learner Details')
y=489
for label,value,name in [
 ('Learner Name and Surname','Mosa Msiza','learner_name'),
 ('Contact Number / Cell','0823131009','learner_phone'),
 ('Email Address','Mosamsiza.29@gmail.com','learner_email'),
 ('Name of Programme','Systems Development','programme'),
 ('Date','30/10/2026','report_date')]:row(label,value,y,name);y-=34
txt(72,294,'Host Details',12,True)
y=279
for label,value,name in [
 ('Company Name','Belgium Campus ITversity','host_name'),
 ('Physical Address','138 Berg Avenue, Heatherdale, Akasia, Pretoria, 0001','host_address'),
 ('Contact Person Name and Surname','Simba Zingeni','supervisor_name'),
 ('Contact Numbers','0671007329','supervisor_phone'),
 ('Email Address','zingeni.s@belgiumcampus.ac.za','supervisor_email')]:row(label,value,y,name);y-=38
c.showPage()

tasks={
5:'Refined nominated tutor cards and learning-resource nominations; corrected session reminders and transcript exemptions for D- modules.',
6:'Added admin tutor creation with student API verification, deregistration audit history, profile onboarding and tutor performance analytics; simplified session reviews.',
7:'Improved administrative access and date filters; developed the super admin compensation dashboard with Excel exports.',
8:'Improved compensation reports and Senior Tutor role support; fixed tutor deactivation/rejoining, optional bio onboarding and availability time selection.',
9:'Added admin announcements with SignalR notifications; separated notification tabs and simplified user and study-area filters.',
12:'Added tutor application pagination, sorting and bulk rejection; refined applicant selection controls.',
13:'Implemented pilot Teams meetings for bookings and confirmation; synchronised cancellations, improved authentication diagnostics and fixed availability redirects.',
14:'No dated project record found. Learner to confirm the work performed on this date.',
15:'Refined Teams button labels, icons and styling; simplified session confirmation with automatic Teams confirmation and expandable manual links.',
16:'Unified one-step Teams confirmation across dashboards and sessions; notified manually added tutors and enabled cross-programme module selection.',
19:'Added Teams recordings and attendance to Tutor Head reviews; refined transcript previews, pilot controls, Graph diagnostics and shared organiser setup.',
20:'Moved Teams account configuration into booking settings; refined Mzala Connect account identity storage.',
21:'Separated booking validation and clarified Teams errors; enabled deregistered tutor reapplication with preserved history and refined prefilled application fields.',
22:'Integrated Teams interview scheduling into tutor applications; refined application review modals and resolved integration conflicts.',
23:'Added a pilot to start a tutoring session automatically when joining Teams; investigated a Docker project-file restore error.',
25:'Refined senior tutor and Head of Tutor application decisions, eligibility handling and rejection behaviour (dated Sunday project commit).',
26:'No dated project record found. Learner to confirm the work performed on this date.',
27:'Improved admin notification links and tutor profile navigation; reviewed shared-organiser meeting attendance behaviour and multiple-report handling.',
28:'Fixed tutor directory back navigation to retain pagination, filters and scroll position.',
29:'Investigated dashboard HTTP 500 errors and possible database migration mismatch; reviewed sessions stuck InProgress and how system emails are sent.',
30:'Prepared the October workplace training log and attendance form using project history for 5-30 October.'
}
base(2,'Workplace Training')
txt(72,492,'Task descriptions are editable. Entries use dated project commits and chat records.',8)
x=72;top=479;w=698;datew=83;rh=18.5
c.setFillColor(gray);c.rect(x,top-25,w,25,fill=1,stroke=1)
txt(x+5,top-16,'DATE',9,True);txt(x+datew+6,top-16,'TASK DESCRIPTION (Experience recorded by Learner)',9,True)
c.line(x+datew,top,x+datew,top-25)
y=top-25
for d,desc in tasks.items():
 c.rect(x,y-rh,w,rh);c.line(x+datew,y,x+datew,y-rh)
 txt(x+4,y-12,f'{d:02d}/10/2026',8)
 lines=simpleSplit(desc,'Helvetica',7.5,w-datew-12)
 assert len(lines)<=2,(d,lines)
 field(f'task_2026_10_{d:02d}',x+datew+4,y-rh+1,w-datew-8,rh-2,'\n'.join(lines),7.5,True)
 y-=rh
c.showPage()

base(3,'Comments by the Supervisor / Manager')
txt(72,488,"Please comment on the learner's performance during the reporting period.",10)
field('supervisor_comments',74,327,693,145,'',10,True)
c.rect(72,326,698,152)
c.line(72,267,340,267);txt(72,252,'Supervisor / Manager Signature',10)
c.line(588,267,770,267);txt(588,252,'Date',10)
c.line(72,195,340,195);txt(72,180,'Student / Trainee Signature',10)
c.line(588,195,770,195);txt(588,180,'Date',10)
c.rect(367,170,190,112);txt(405,220,'Company Stamp',11)
txt(72,130,'NB:',10,True)
txt(72,112,'The supervisor must complete the comments section and sign the log sheet.',9)
txt(72,96,'Insert the host company letterhead or place the company stamp in the area provided.',9)
c.showPage()

base(4,'Attendance Register')
txt(72,489,'Name and Surname: Mosa Msiza',11,True)
txt(72,473,'Estimated times for learner review: varied within supplied ranges; lunch assumed at 12:00.',8)
xs=[72,210,333,463,590,770];top=459;head=23;rh=13.1
c.setFillColor(gray);c.rect(72,top-head,698,head,fill=1,stroke=1)
for i,s in enumerate(['Date','Time In','Lunch','Time Out','Signature']):txt(xs[i]+5,top-15,s,10,True)
for x in xs[1:-1]:c.line(x,top,x,top-head)
y=top-head
attendance_rng=Random(20261030)
attendance_values={}
for n in range(26):
 day=date(2026,10,5)+timedelta(days=n)
 weekend=day.weekday()>=5
 if weekend:c.setFillColor(gray);c.rect(72,y-rh,698,rh,fill=1,stroke=0)
 c.rect(72,y-rh,698,rh)
 for x in xs[1:-1]:c.line(x,y,x,y-rh)
 txt(77,y-9.5,day.strftime('%d/%m/%Y')+(' '+day.strftime('%a') if weekend else ''),8,weekend)
 if not weekend:
  out_minutes=30+attendance_rng.randint(0,30)
  times=[f'08:{attendance_rng.randint(0,30):02d}','12:00',f'{16+out_minutes//60:02d}:{out_minutes%60:02d}']
  for j,key in enumerate(['time_in','lunch','time_out']):
   name=f'{key}_{day.isoformat()}'
   attendance_values[name]=times[j]
   field(name,xs[j+1]+3,y-rh+1,xs[j+2]-xs[j+1]-6,rh-2,times[j],8)
 y-=rh
txt(72,72,'Supervisor / Manager Signature:',10,True);c.line(246,70,480,70)
c.showPage();c.save()
r=PdfReader(OUT)
assert len(r.pages)==4
fs=r.get_fields();assert len(fs)==92,len(fs)
assert fs['learner_name']['/V']=='Mosa Msiza'
assert fs['report_date']['/V']=='30/10/2026'
assert len(attendance_values)==60
for name,value in attendance_values.items():assert fs[name]['/V']==value
widgets=0
for p in r.pages:
 for ref in p.get('/Annots',[]):
  a=ref.get_object()
  if a.get('/Subtype')=='/Widget':
   widgets+=1;assert a.get('/T') in fs;assert '/N' in a['/AP'];assert a.get('/V')==fs[a['/T']].get('/V')
assert widgets==len(fs)
print(OUT);print(f'Validated: 4 pages, {len(fs)} editable fields, all widget values and appearances consistent.')
