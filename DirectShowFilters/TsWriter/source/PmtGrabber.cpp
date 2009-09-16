/* 
 *	Copyright (C) 2006-2008 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */
#pragma warning(disable : 4995)
#include <windows.h>
#include <commdlg.h>
#include <bdatypes.h>
#include <time.h>
#include <streams.h>
#include <initguid.h>

#include "pmtgrabber.h"


extern void LogDebug(const char *fmt, ...) ;


CPmtGrabber::CPmtGrabber(LPUNKNOWN pUnk, HRESULT *phr) 
:CUnknown( NAME ("MpTsPmtGrabber"), pUnk)
{
	m_pCallback=NULL;
	m_iPmtVersion=-1;
	m_iServiceId=0;
	memset(m_pmtPrevData,0,sizeof(m_pmtPrevData));
}
CPmtGrabber::~CPmtGrabber(void)
{
}


STDMETHODIMP CPmtGrabber::SetPmtPid( int pmtPid, long serviceId)
{
	try
	{
    GetPMTPid=false;
		CEnterCriticalSection enter(m_section);
		LogDebug("pmtgrabber: grab pmt:%x sid:%x", pmtPid,serviceId);
		CSectionDecoder::Reset();
    if (pmtPid==0)
    {
      // need to Grab PMT pid
      LogDebug("PMT Pid is not set, Searching PAT for PMT Pid.");
      CSectionDecoder::SetPid(0x0);
      GetPMTPid=true; 
      m_iServiceId=serviceId;
    }
    else
    {
    	CSectionDecoder::SetPid(pmtPid);
    	m_iPmtVersion=-1;
    	m_iServiceId=serviceId;
    	memset(m_pmtPrevData,0,sizeof(m_pmtPrevData));
  }
  }
	catch(...)
	{
		LogDebug("CPmtGrabber::SetPmtPid exception");
	}
	return S_OK;
}

STDMETHODIMP CPmtGrabber::SetCallBack( IPMTCallback* callback)
{
	CEnterCriticalSection enter(m_section);
	LogDebug("pmtgrabber: set callback:%x", callback);
	m_pCallback=callback;
	return S_OK;
}

void CPmtGrabber::OnTsPacket(byte* tsPacket)
{
	if (m_pCallback==NULL) return;
  int pid=((tsPacket[1] & 0x1F) <<8)+tsPacket[2];
  if (pid != GetPid()) return;
	CEnterCriticalSection enter(m_section);
	CSectionDecoder::OnTsPacket(tsPacket);
}

void CPmtGrabber::OnNewSection(CSection& section)
{
	try
	{
    int PMTPid;
    if(GetPMTPid==true)
	    {
        PMTPid=m_patgrab.PATRequest(section, m_iServiceId);
        if (PMTPid!=0)
        {
          if (PMTPid==-1)
          {
            LogDebug("PMT Pid wasn't found on the PAT. Channel may have moved. Try a new channel search");
          }
          else
          {
            LogDebug("Got PMT Pid - %x",PMTPid);
          }
          SetPmtPid(PMTPid,m_iServiceId);
          SetPid(PMTPid);
        }
        return;			
      }
		if (section.table_id!=2) return;
		//if (section.version_number == m_iPmtVersion) return;
	  CEnterCriticalSection enter(m_section);

		if (section.section_length<0 || section.section_length>=MAX_SECTION_LENGTH) return;

		long serviceId = section.table_id_extension;
    if (m_iPmtVersion<0)
		  LogDebug("pmtgrabber: got pmt %x sid:%x",GetPid(), serviceId);

		if (serviceId != m_iServiceId) 
		{	
			LogDebug("pmtgrabber: serviceid mismatch %d != %d",serviceId,m_iServiceId);
			return;
		}

		m_iPmtLength=section.section_length;

		memcpy(m_pmtData,section.Data,m_iPmtLength);
		if (memcmp(m_pmtData,m_pmtPrevData,m_iPmtLength)!=0)
		{
			memcpy(m_pmtPrevData,m_pmtData,m_iPmtLength);
      // do a callback each time the version number changes. this also allows switching for "regional channels"
			if (m_pCallback!=NULL && m_iPmtVersion != section.version_number)
			{
        LogDebug("pmtgrabber: got new pmt version:%d %d, service_id:%d", section.version_number, m_iPmtVersion, serviceId);
				LogDebug("pmtgrabber: do callback pid = %x",GetPid());
				m_pCallback->OnPMTReceived(GetPid());
			}
		}
 		m_iPmtVersion=section.version_number;
	}
	catch(...)
	{
		LogDebug("CPmtGrabber::OnNewSection exception");
	}
}

STDMETHODIMP CPmtGrabber::GetPMTData(BYTE *pmtData)
{
	try
	{
	  CEnterCriticalSection enter(m_section);
		if (m_iPmtLength>0)
		{
			memcpy(pmtData,m_pmtData,m_iPmtLength);
			return m_iPmtLength;
		}
	}
	catch(...)
	{
		LogDebug("CPmtGrabber::GetPMTData exception");
	}
	return 0;
}
