import json
import textwrap
from os.path import exists
from sys import argv

from revChatGPT import Chatbot
from flask import Flask
from flask import request

class CustomPromptInjection(Flask):

    chatbot = None

    prompts = [
        '''You will be supporting customers of Azure with their problems. For each problem, first try to see if the problem is one of those which I provide below. If it is, use the solution that I provide below. If not, refer the customer to the most relevant part of Azure Service Health documentation, be as specific as possible
 
List of possible problems and their solutions:
 
Problem 1: User can't find any health event posted in the Service Issues blade, but they think they're impacted. The user might not be able to access a VM or any other Azure resource. The user might also want to see all the outages or events that are affecting them.
 
Solution 1: Help the user find service health events that are impacting their subscriptions. Start by offering to run the Azure Resource Graph query:
ServiceHealthResources
| where type =~ 'Microsoft.ResourceHealth/events'
| extend eventType = tostring(properties.EventType), status = properties.Status, description = properties.Title, trackingId = properties.TrackingId, summary = properties.Summary, priority = properties.Priority, impactStartTime = properties.ImpactStartTime, impactMitigationTime = todatetime(tolong(properties.ImpactMitigationTime))
| where properties.Status == 'Active' and tolong(impactStartTime) > 1
| summarize count(subscriptionId) by name, eventType
 
If that helped, notify the user that the subscription filter might have been set. If it didn't help, let the user know there might have been an outage that just ended, or that there is an outage which was not posted yet
 
Do you understand?'''
    ]
    
    def run(self, host=None, port=None, debug=None, load_dotenv=True, **options):
        print("hi how are you")
        if exists("config.json"):
            with open("config.json", encoding="utf-8") as f:
                config = json.load(f)
                self.chatbot = Chatbot(config, debug=True,)
                #self.chatbot.reset_chat()
                #self.chatbot.refresh_session()
                for p in self.prompts:
                    answer = app.chatbot.get_chat_response(p, output="text")
                    print (answer)
            
        super(CustomPromptInjection,self).run(host=host, port=port, debug=debug, load_dotenv=load_dotenv, **options)

app = CustomPromptInjection(__name__)

@app.route("/api/Chatbot", methods=['GET', 'POST'])
def hello_world():
    
    if request.method == 'GET':
        question = request.args.get('question')
    if request.method == 'POST':    
        question = request.form['question']
    if question is None:
        return "No question? have you run out of words puny mortals?"    
    
    answer = app.chatbot.get_chat_response(question, output="text")
    return answer

app.run()